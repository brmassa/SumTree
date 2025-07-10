using SumTree.Cursors;

namespace SumTree;

/// <summary>
/// Extension methods for creating cursors on SumTree instances.
/// </summary>
public static class SumTreeCursorExtensions
{
    /// <summary>
    /// Creates a cursor for the specified SumTree using the given dimension.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to create a cursor for.</param>
    /// <param name="dimension">The dimension to use for seeking.</param>
    /// <returns>A new cursor for the tree.</returns>
    public static ICursor<T, TDimension> Cursor<T, TDimension>(
        this SumTree<T> tree,
        ISummaryDimension<T, TDimension> dimension)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        return new SumTreeCursor<T, TDimension>(tree, dimension);
    }

    /// <summary>
    /// Creates a cursor for the specified SumTree using an existing dimension.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to create a cursor for.</param>
    /// <returns>A new cursor for the tree using the existing dimension.</returns>
    public static ICursor<T, TDimension> Cursor<T, TDimension>(this SumTree<T> tree)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        if (!tree.TryGetSummary<TDimension>(out var summary))
        {
            throw new InvalidOperationException($"Tree does not have dimension of type {typeof(TDimension).Name}");
        }

        // We need to get the dimension instance from the tree
        var dimensions = tree.GetDimensions();
        if (dimensions != null && dimensions.TryGetValue(typeof(TDimension), out var dimensionObj) &&
            dimensionObj is ISummaryDimension<T, TDimension> dimension)
        {
            return new SumTreeCursor<T, TDimension>(tree, dimension);
        }

        throw new InvalidOperationException($"Could not find dimension instance for type {typeof(TDimension).Name}");
    }

    /// <summary>
    /// Creates a cursor for character-based trees using line number dimension.
    /// </summary>
    /// <param name="tree">The character tree to create a cursor for.</param>
    /// <returns>A new cursor for line-based navigation.</returns>
    public static ICursor<char, LineNumberSummary> LineCursor(this SumTree<char> tree)
    {
        return tree.Cursor(LineNumberDimension.Instance);
    }

    /// <summary>
    /// Creates a cursor for character-based trees using bracket count dimension.
    /// </summary>
    /// <param name="tree">The character tree to create a cursor for.</param>
    /// <returns>A new cursor for bracket-based navigation.</returns>
    public static ICursor<char, BracketCountSummary> BracketCursor(this SumTree<char> tree)
    {
        return tree.Cursor(BracketCountDimension.Instance);
    }

    /// <summary>
    /// Seeks to a specific position in the tree using the specified dimension.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to seek in.</param>
    /// <param name="dimension">The dimension to use for seeking.</param>
    /// <param name="target">The target position to seek to.</param>
    /// <param name="bias">The bias for positioning when target is between items.</param>
    /// <returns>A tuple containing the found item and whether it was found.</returns>
    public static (T? item, bool found) SeekTo<T, TDimension>(
        this SumTree<T> tree,
        ISummaryDimension<T, TDimension> dimension,
        TDimension target,
        Bias bias = Bias.Left)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        using var cursor = tree.Cursor(dimension);
        cursor.Seek(target, bias);
        return (cursor.Item, !cursor.IsAtEnd);
    }

    /// <summary>
    /// Finds the first position in the tree where the predicate returns true.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to search in.</param>
    /// <param name="dimension">The dimension to use for searching.</param>
    /// <param name="predicate">The predicate to test each position.</param>
    /// <returns>The position where the predicate returned true, or null if not found.</returns>
    public static TDimension? FindFirst<T, TDimension>(
        this SumTree<T> tree,
        ISummaryDimension<T, TDimension> dimension,
        Func<TDimension, bool> predicate)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        using var cursor = tree.Cursor(dimension);
        cursor.Start();

        while (!cursor.IsAtEnd)
        {
            if (predicate(cursor.Position))
            {
                return cursor.Position;
            }

            if (!cursor.Next()) break;
        }

        return default;
    }

    /// <summary>
    /// Finds the last position in the tree where the predicate returns true.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to search in.</param>
    /// <param name="dimension">The dimension to use for searching.</param>
    /// <param name="predicate">The predicate to test each position.</param>
    /// <returns>The position where the predicate returned true, or null if not found.</returns>
    public static TDimension? FindLast<T, TDimension>(
        this SumTree<T> tree,
        ISummaryDimension<T, TDimension> dimension,
        Func<TDimension, bool> predicate)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        using var cursor = tree.Cursor(dimension);
        cursor.Seek(cursor.End, Bias.Right);

        TDimension? lastMatch = default;

        while (!cursor.IsAtStart)
        {
            if (!cursor.Previous()) break;

            if (predicate(cursor.Position))
            {
                lastMatch = cursor.Position;
            }
        }

        return lastMatch;
    }

    /// <summary>
    /// Slices the tree from the current position to the specified target using a cursor.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to slice.</param>
    /// <param name="dimension">The dimension to use for slicing.</param>
    /// <param name="start">The start position for the slice.</param>
    /// <param name="end">The end position for the slice.</param>
    /// <returns>A new tree containing the sliced range.</returns>
    public static SumTree<T> SliceRange<T, TDimension>(
        this SumTree<T> tree,
        ISummaryDimension<T, TDimension> dimension,
        TDimension start,
        TDimension end)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        using var cursor = tree.Cursor(dimension);
        cursor.Seek(start, Bias.Left);
        return cursor.Slice(end, Bias.Left);
    }

    /// <summary>
    /// Creates an enumerable that iterates through the tree using a cursor.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to iterate through.</param>
    /// <param name="dimension">The dimension to use for iteration.</param>
    /// <returns>An enumerable for cursor-based iteration.</returns>
    public static IEnumerable<T> CursorIterate<T, TDimension>(
        this SumTree<T> tree,
        ISummaryDimension<T, TDimension> dimension)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        using var cursor = tree.Cursor(dimension);
        cursor.Start();

        while (!cursor.IsAtEnd)
        {
            if (cursor.Item != null)
            {
                yield return cursor.Item;
            }

            if (!cursor.Next()) break;
        }
    }

    /// <summary>
    /// Creates an enumerable that iterates through positions in the tree using a cursor.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to iterate through.</param>
    /// <param name="dimension">The dimension to use for iteration.</param>
    /// <returns>An enumerable for cursor-based position iteration.</returns>
    public static IEnumerable<(T item, TDimension position)> CursorIterateWithPosition<T, TDimension>(
        this SumTree<T> tree,
        ISummaryDimension<T, TDimension> dimension)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        using var cursor = tree.Cursor(dimension);
        cursor.Start();

        while (!cursor.IsAtEnd)
        {
            if (cursor.Item != null)
            {
                yield return (cursor.Item, cursor.Position);
            }

            if (!cursor.Next()) break;
        }
    }

    /// <summary>
    /// Filters the tree using a cursor and returns a new tree with only matching elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the tree.</typeparam>
    /// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
    /// <param name="tree">The tree to filter.</param>
    /// <param name="dimension">The dimension to use for filtering.</param>
    /// <param name="predicate">The predicate to test each element.</param>
    /// <returns>A new tree containing only the matching elements.</returns>
    public static SumTree<T> CursorFilter<T, TDimension>(
        this SumTree<T> tree,
        ISummaryDimension<T, TDimension> dimension,
        Func<T, bool> predicate)
        where T : IEquatable<T>
        where TDimension : IEquatable<TDimension>
    {
        var result = new List<T>();

        using var cursor = tree.Cursor(dimension);
        cursor.Start();

        while (!cursor.IsAtEnd)
        {
            if (cursor.Item != null && predicate(cursor.Item))
            {
                result.Add(cursor.Item);
            }

            if (!cursor.Next()) break;
        }

        return result.ToSumTree().WithDimension(dimension);
    }
}
