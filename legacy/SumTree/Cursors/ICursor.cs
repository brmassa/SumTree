namespace SumTree.Cursors;

/// <summary>
/// Defines the contract for cursor-based navigation through a SumTree.
/// Cursors provide efficient traversal, seeking, and slicing operations.
/// </summary>
/// <typeparam name="T">The type of elements in the tree.</typeparam>
/// <typeparam name="TDimension">The dimension type used for seeking.</typeparam>
public interface ICursor<T, TDimension> : IDisposable
    where T : IEquatable<T>
    where TDimension : IEquatable<TDimension>
{
    /// <summary>
    /// Gets the current item at the cursor position.
    /// </summary>
    /// <returns>The current item, or default if at end.</returns>
    T? Item { get; }

    /// <summary>
    /// Gets the summary of the current item.
    /// </summary>
    /// <returns>The item's summary.</returns>
    TDimension? ItemSummary { get; }

    /// <summary>
    /// Gets whether the cursor is at the beginning of the tree.
    /// </summary>
    bool IsAtStart { get; }

    /// <summary>
    /// Gets whether the cursor is at the end of the tree.
    /// </summary>
    bool IsAtEnd { get; }

    /// <summary>
    /// Gets the current position summary from the start of the tree.
    /// </summary>
    TDimension Position { get; }

    /// <summary>
    /// Gets the end position summary for the entire tree.
    /// </summary>
    TDimension End { get; }

    /// <summary>
    /// Moves the cursor to the start of the tree.
    /// </summary>
    void Start();

    /// <summary>
    /// Moves the cursor to the next item.
    /// </summary>
    /// <returns>True if moved to a valid item, false if reached end.</returns>
    bool Next();

    /// <summary>
    /// Moves the cursor to the previous item.
    /// </summary>
    /// <returns>True if moved to a valid item, false if reached start.</returns>
    bool Previous();

    /// <summary>
    /// Seeks to a specific position in the tree.
    /// </summary>
    /// <param name="target">The target position to seek to.</param>
    /// <param name="bias">The bias for positioning when target is between items.</param>
    void Seek(TDimension target, Bias bias = Bias.Left);

    /// <summary>
    /// Seeks forward from the current position.
    /// </summary>
    /// <param name="target">The target position to seek to.</param>
    /// <param name="bias">The bias for positioning when target is between items.</param>
    void SeekForward(TDimension target, Bias bias = Bias.Left);

    /// <summary>
    /// Creates a slice from the current position to the specified target.
    /// </summary>
    /// <param name="target">The end position for the slice.</param>
    /// <param name="bias">The bias for the end position.</param>
    /// <returns>A new SumTree containing the sliced range.</returns>
    SumTree<T> Slice(TDimension target, Bias bias = Bias.Left);

    /// <summary>
    /// Creates a slice from the current position to the end of the tree.
    /// </summary>
    /// <returns>A new SumTree containing the suffix.</returns>
    SumTree<T> Suffix();

    /// <summary>
    /// Gets the summary from the current position to the end of the tree.
    /// </summary>
    /// <returns>The summary of the remaining elements.</returns>
    TDimension Summary();

    /// <summary>
    /// Searches backward from the current position using a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to test each position.</param>
    /// <returns>True if a matching position was found.</returns>
    bool SearchBackward(Func<TDimension, bool> predicate);

    /// <summary>
    /// Searches forward from the current position using a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to test each position.</param>
    /// <returns>True if a matching position was found.</returns>
    bool SearchForward(Func<TDimension, bool> predicate);

    /// <summary>
    /// Clones the current cursor state.
    /// </summary>
    /// <returns>A new cursor with the same position and state.</returns>
    ICursor<T, TDimension> Clone();
}

/// <summary>
/// Defines bias for cursor positioning when seeking between items.
/// </summary>
public enum Bias
{
    /// <summary>
    /// Position the cursor before the target position.
    /// </summary>
    Left,

    /// <summary>
    /// Position the cursor after the target position.
    /// </summary>
    Right
}
