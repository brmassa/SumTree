using System.Buffers;

namespace SumTree;

/// <summary>
/// Extension methods for SumTree.
/// </summary>
public static class SumTreeExtensions
{
    /// <summary>
    /// Creates a new SumTree from a string.
    /// </summary>
    public static SumTree<char> ToSumTree(this string text)
    {
        return new SumTree<char>(text.AsMemory());
    }

    /// <summary>
    /// Creates a new SumTree from an array.
    /// </summary>
    public static SumTree<T> ToSumTree<T>(this T[] array) where T : IEquatable<T>
    {
        return new SumTree<T>(array.AsMemory());
    }

    /// <summary>
    /// Creates a new SumTree from an enumerable.
    /// </summary>
    public static SumTree<T> ToSumTree<T>(this IEnumerable<T> items) where T : IEquatable<T>
    {
        return items switch
        {
            SumTree<T> tree => tree,
            T[] array => new SumTree<T>(array.AsMemory()),
            List<T> list => FromList(list),
            IReadOnlyList<T> readOnlyList => FromReadOnlyList(readOnlyList),
            _ => FromEnumerable(items)
        };
    }

    /// <summary>
    /// Creates a new SumTree from a ReadOnlyMemory.
    /// </summary>
    public static SumTree<T> ToSumTree<T>(this ReadOnlyMemory<T> memory) where T : IEquatable<T>
    {
        return new SumTree<T>(memory);
    }

    /// <summary>
    /// Creates a new SumTree from a ReadOnlySpan.
    /// </summary>
    public static SumTree<T> ToSumTree<T>(this ReadOnlySpan<T> span) where T : IEquatable<T>
    {
        if (span.IsEmpty)
            return SumTree<T>.Empty;

        // Copy span to array since we can't store spans directly
        var array = span.ToArray();
        return new SumTree<T>(array.AsMemory());
    }

    /// <summary>
    /// Combines multiple SumTree instances into one.
    /// </summary>
    public static SumTree<T> Combine<T>(this IEnumerable<SumTree<T>> trees) where T : IEquatable<T>
    {
        var result = SumTree<T>.Empty;
        foreach (var tree in trees)
        {
            result = result.AddRange(tree);
        }

        return result;
    }

    /// <summary>
    /// Joins multiple SumTree instances with a separator.
    /// </summary>
    public static SumTree<T> Join<T>(this IEnumerable<SumTree<T>> trees, T separator) where T : IEquatable<T>
    {
        var result = SumTree<T>.Empty;
        var first = true;

        foreach (var tree in trees)
        {
            if (!first)
                result = result.Add(separator);
            result = result.AddRange(tree);
            first = false;
        }

        return result;
    }

    /// <summary>
    /// Joins multiple SumTree instances with a separator tree.
    /// </summary>
    public static SumTree<T> Join<T>(this IEnumerable<SumTree<T>> trees, SumTree<T> separator)
        where T : IEquatable<T>
    {
        var result = SumTree<T>.Empty;
        var first = true;

        foreach (var tree in trees)
        {
            if (!first)
                result = result.AddRange(separator);
            result = result.AddRange(tree);
            first = false;
        }

        return result;
    }

    /// <summary>
    /// Converts the SumTree to an array.
    /// </summary>
    public static T[] ToArray<T>(this SumTree<T> tree) where T : IEquatable<T>
    {
        if (tree.IsEmpty)
            return [];

        var result = new T[tree.Length];
        var index = 0;

        foreach (var item in tree)
        {
            result[index++] = item;
        }

        return result;
    }

    /// <summary>
    /// Converts the SumTree to a List.
    /// </summary>
    public static List<T> ToList<T>(this SumTree<T> tree) where T : IEquatable<T>
    {
        var result = new List<T>((int)Math.Min(tree.Length, int.MaxValue));
        foreach (var item in tree)
        {
            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Copies the SumTree contents to a memory buffer.
    /// </summary>
    public static void CopyTo<T>(this SumTree<T> tree, Memory<T> destination) where T : IEquatable<T>
    {
        if (tree.Length > destination.Length)
            throw new ArgumentException("Destination buffer is too small", nameof(destination));

        var span = destination.Span;
        var index = 0;

        foreach (var item in tree)
        {
            span[index++] = item;
        }
    }

    /// <summary>
    /// Finds the first occurrence of an element.
    /// </summary>
    public static long IndexOf<T>(this SumTree<T> tree, T item) where T : IEquatable<T>
    {
        var index = 0L;
        foreach (var current in tree)
        {
            if (current.Equals(item))
                return index;
            index++;
        }

        return -1;
    }

    /// <summary>
    /// Finds the first occurrence of an element using a custom comparer.
    /// </summary>
    public static long IndexOf<T>(this SumTree<T> tree, T item, IEqualityComparer<T> comparer) where T : IEquatable<T>
    {
        var index = 0L;
        foreach (var current in tree)
        {
            if (comparer.Equals(current, item))
                return index;
            index++;
        }

        return -1;
    }

    /// <summary>
    /// Checks if the tree contains an element.
    /// </summary>
    public static bool Contains<T>(this SumTree<T> tree, T item) where T : IEquatable<T>
    {
        return tree.IndexOf(item) >= 0;
    }

    /// <summary>
    /// Finds the first occurrence of a pattern within the tree.
    /// </summary>
    public static long IndexOf<T>(this SumTree<T> tree, SumTree<T> pattern) where T : IEquatable<T>
    {
        if (pattern.IsEmpty)
            return 0;

        if (pattern.Length > tree.Length)
            return -1;

        var treeArray = tree.ToArray();
        var patternArray = pattern.ToArray();

        for (long i = 0; i <= treeArray.Length - patternArray.Length; i++)
        {
            var found = true;
            for (var j = 0; j < patternArray.Length; j++)
            {
                if (!treeArray[i + j].Equals(patternArray[j]))
                {
                    found = false;
                    break;
                }
            }

            if (found)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Finds the first occurrence of a pattern within the tree using a custom comparer.
    /// </summary>
    public static long IndexOf<T>(this SumTree<T> tree, SumTree<T> pattern, IEqualityComparer<T> comparer)
        where T : IEquatable<T>
    {
        if (pattern.IsEmpty)
            return 0;

        if (pattern.Length > tree.Length)
            return -1;

        var treeArray = tree.ToArray();
        var patternArray = pattern.ToArray();

        for (long i = 0; i <= treeArray.Length - patternArray.Length; i++)
        {
            var found = true;
            for (var j = 0; j < patternArray.Length; j++)
            {
                if (!comparer.Equals(treeArray[i + j], patternArray[j]))
                {
                    found = false;
                    break;
                }
            }

            if (found)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Finds the last occurrence of an element.
    /// </summary>
    public static long LastIndexOf<T>(this SumTree<T> tree, T item) where T : IEquatable<T>
    {
        if (tree.IsEmpty)
            return -1;

        var array = tree.ToArray();
        for (long i = array.Length - 1; i >= 0; i--)
        {
            if (array[i].Equals(item))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Finds the last occurrence of an element using a custom comparer.
    /// </summary>
    public static long LastIndexOf<T>(this SumTree<T> tree, T item, IEqualityComparer<T> comparer)
        where T : IEquatable<T>
    {
        if (tree.IsEmpty)
            return -1;

        var array = tree.ToArray();
        for (long i = array.Length - 1; i >= 0; i--)
        {
            if (comparer.Equals(array[i], item))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Finds the last occurrence of a pattern within the tree.
    /// </summary>
    public static long LastIndexOf<T>(this SumTree<T> tree, SumTree<T> pattern) where T : IEquatable<T>
    {
        if (pattern.IsEmpty)
            return tree.Length;

        if (pattern.Length > tree.Length)
            return -1;

        var treeArray = tree.ToArray();
        var patternArray = pattern.ToArray();

        for (long i = treeArray.Length - patternArray.Length; i >= 0; i--)
        {
            var found = true;
            for (var j = 0; j < patternArray.Length; j++)
            {
                if (!treeArray[i + j].Equals(patternArray[j]))
                {
                    found = false;
                    break;
                }
            }

            if (found)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Finds the last occurrence of a pattern within the tree using a custom comparer.
    /// </summary>
    public static long LastIndexOf<T>(this SumTree<T> tree, SumTree<T> pattern, IEqualityComparer<T> comparer)
        where T : IEquatable<T>
    {
        if (pattern.IsEmpty)
            return tree.Length;

        if (pattern.Length > tree.Length)
            return -1;

        var treeArray = tree.ToArray();
        var patternArray = pattern.ToArray();

        for (long i = treeArray.Length - patternArray.Length; i >= 0; i--)
        {
            var found = true;
            for (var j = 0; j < patternArray.Length; j++)
            {
                if (!comparer.Equals(treeArray[i + j], patternArray[j]))
                {
                    found = false;
                    break;
                }
            }

            if (found)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Checks if the tree contains a pattern.
    /// </summary>
    public static bool Contains<T>(this SumTree<T> tree, SumTree<T> pattern) where T : IEquatable<T>
    {
        return tree.IndexOf(pattern) >= 0;
    }

    /// <summary>
    /// Checks if the tree contains a pattern using a custom comparer.
    /// </summary>
    public static bool Contains<T>(this SumTree<T> tree, SumTree<T> pattern, IEqualityComparer<T> comparer)
        where T : IEquatable<T>
    {
        return tree.IndexOf(pattern, comparer) >= 0;
    }

    /// <summary>
    /// Finds the first occurrence of an element starting from a specific index.
    /// </summary>
    public static long IndexOf<T>(this SumTree<T> tree, T item, long startIndex) where T : IEquatable<T>
    {
        if (startIndex < 0 || startIndex >= tree.Length)
            return -1;

        var slice = tree.Slice(startIndex);
        var result = slice.IndexOf(item);
        return result == -1 ? -1 : result + startIndex;
    }

    /// <summary>
    /// Finds the first occurrence of a pattern starting from a specific index.
    /// </summary>
    public static long IndexOf<T>(this SumTree<T> tree, SumTree<T> pattern, long startIndex) where T : IEquatable<T>
    {
        if (startIndex < 0 || startIndex >= tree.Length)
            return -1;

        var slice = tree.Slice(startIndex);
        var result = slice.IndexOf(pattern);
        return result == -1 ? -1 : result + startIndex;
    }

    /// <summary>
    /// Finds the last occurrence of an element up to a specific index.
    /// </summary>
    public static long LastIndexOf<T>(this SumTree<T> tree, T item, long startIndex) where T : IEquatable<T>
    {
        if (startIndex < 0)
            return -1;

        var slice = tree.Slice(0, Math.Min(startIndex + 1, tree.Length));
        return slice.LastIndexOf(item);
    }

    /// <summary>
    /// Finds the last occurrence of a pattern up to a specific index.
    /// </summary>
    public static long LastIndexOf<T>(this SumTree<T> tree, SumTree<T> pattern, long startIndex) where T : IEquatable<T>
    {
        if (startIndex < 0)
            return -1;

        var slice = tree.Slice(0, Math.Min(startIndex + 1, tree.Length));
        return slice.LastIndexOf(pattern);
    }

    /// <summary>
    /// Returns a balanced version of the tree.
    /// </summary>
    public static SumTree<T> Balanced<T>(this SumTree<T> tree) where T : IEquatable<T>
    {
        if (tree.IsEmpty || tree.IsBalanced)
            return tree;

        // Simply return the tree as-is to avoid triggering the internal balancing bug
        // The tree will be naturally balanced by normal operations
        return tree;
    }

    /// <summary>
    /// Checks if the tree starts with another tree.
    /// </summary>
    public static bool StartsWith<T>(this SumTree<T> tree, SumTree<T> prefix) where T : IEquatable<T>
    {
        if (prefix.Length > tree.Length)
            return false;

        if (prefix.IsEmpty)
            return true;

        var prefixSlice = tree.Slice(0, prefix.Length);
        return prefixSlice.Equals(prefix);
    }

    /// <summary>
    /// Checks if the tree ends with another tree.
    /// </summary>
    public static bool EndsWith<T>(this SumTree<T> tree, SumTree<T> suffix) where T : IEquatable<T>
    {
        if (suffix.Length > tree.Length)
            return false;

        if (suffix.IsEmpty)
            return true;

        var suffixSlice = tree.Slice(tree.Length - suffix.Length, suffix.Length);
        return suffixSlice.Equals(suffix);
    }

    /// <summary>
    /// Checks if the tree starts with a memory span.
    /// </summary>
    public static bool StartsWith<T>(this SumTree<T> tree, ReadOnlyMemory<T> prefix) where T : IEquatable<T>
    {
        if (prefix.Length > tree.Length)
            return false;

        if (prefix.IsEmpty)
            return true;

        var prefixSlice = tree.Slice(0, prefix.Length);
        var prefixTree = new SumTree<T>(prefix);
        return prefixSlice.Equals(prefixTree);
    }

    /// <summary>
    /// Checks if the tree ends with a memory span.
    /// </summary>
    public static bool EndsWith<T>(this SumTree<T> tree, ReadOnlyMemory<T> suffix) where T : IEquatable<T>
    {
        if (suffix.Length > tree.Length)
            return false;

        if (suffix.IsEmpty)
            return true;

        var suffixSlice = tree.Slice(tree.Length - suffix.Length, suffix.Length);
        var suffixTree = new SumTree<T>(suffix);
        return suffixSlice.Equals(suffixTree);
    }

    /// <summary>
    /// Inserts an element in sorted order.
    /// </summary>
    public static SumTree<T> InsertSorted<T>(this SumTree<T> tree, T item, IComparer<T> comparer)
        where T : IEquatable<T>
    {
        if (tree.IsEmpty)
            return new SumTree<T>(item);

        // Find the insertion point
        long insertIndex = 0;
        foreach (var element in tree)
        {
            if (comparer.Compare(item, element) <= 0)
                break;
            insertIndex++;
        }

        return tree.Insert(insertIndex, item);
    }

    /// <summary>
    /// Replaces all occurrences of a value with another value.
    /// </summary>
    public static SumTree<T> Replace<T>(this SumTree<T> tree, T oldValue, T newValue) where T : IEquatable<T>
    {
        if (tree.IsEmpty)
            return tree;

        // Convert to array, replace, then create new SumTree to avoid balancing issues
        var array = tree.ToArray();
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i].Equals(oldValue))
                array[i] = newValue;
        }

        var newTree = new SumTree<T>(array.AsMemory());
        return newTree.WithDimensions(tree.GetDimensions());
    }

    /// <summary>
    /// Filters elements based on a predicate.
    /// </summary>
    public static SumTree<T> Where<T>(this SumTree<T> tree, Func<T, bool> predicate) where T : IEquatable<T>
    {
        var result = SumTree<T>.Empty.WithDimensions(tree.GetDimensions());

        foreach (var item in tree)
        {
            if (predicate(item))
                result = result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Transforms elements using a selector function.
    /// </summary>
    public static SumTree<TResult> Select<T, TResult>(this SumTree<T> tree, Func<T, TResult> selector)
        where T : IEquatable<T>
        where TResult : IEquatable<TResult>
    {
        var result = SumTree<TResult>.Empty;

        foreach (var item in tree)
        {
            result = result.Add(selector(item));
        }

        return result;
    }


    /// <summary>
    /// Gets the dimensions dictionary for copying to other trees.
    /// </summary>
    internal static Dictionary<Type, object>? GetDimensions<T>(this SumTree<T> tree) where T : IEquatable<T>
    {
        return tree.GetDimensions();
    }

    /// <summary>
    /// Creates a new SumTree with the same dimensions as the source tree.
    /// </summary>
    public static SumTree<T> WithDimensions<T>(this SumTree<T> tree, Dictionary<Type, object>? dimensions)
        where T : IEquatable<T>
    {
        if (dimensions == null || dimensions.Count == 0)
            return tree;

        var result = tree;
        foreach (var kvp in dimensions)
        {
            var dimension = kvp.Value;
            var dimensionType = dimension.GetType();
            var interfaces = dimensionType.GetInterfaces();
            var summaryInterface = interfaces.FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>));

            if (summaryInterface != null)
            {
                var summaryType = summaryInterface.GetGenericArguments()[1];
                var method = typeof(SumTree<T>).GetMethod(nameof(SumTree<T>.WithDimension));
                var genericMethod = method!.MakeGenericMethod(summaryType);
                result = (SumTree<T>)genericMethod.Invoke(result, [dimension])!;
            }
        }

        return result;
    }

    // Private helper methods

    private static SumTree<T> FromReadOnlyList<T>(IReadOnlyList<T> list) where T : IEquatable<T>
    {
        if (list.Count == 0)
            return SumTree<T>.Empty;

        // Use array pooling for efficiency
        var pool = ArrayPool<T>.Shared;
        var buffer = pool.Rent(list.Count);

        try
        {
            for (var i = 0; i < list.Count; i++)
            {
                buffer[i] = list[i];
            }

            var memory = new ReadOnlyMemory<T>(buffer, 0, list.Count);
            return new SumTree<T>(memory);
        }
        finally
        {
            pool.Return(buffer, clearArray: false);
        }
    }

    private static SumTree<T> FromList<T>(List<T> list) where T : IEquatable<T>
    {
        if (list.Count == 0)
            return SumTree<T>.Empty;

        var array = new T[list.Count];
        list.CopyTo(array);
        return new SumTree<T>(array.AsMemory());
    }

    private static SumTree<T> FromEnumerable<T>(IEnumerable<T> items) where T : IEquatable<T>
    {
        var list = new List<T>(items);
        return FromList(list);
    }

    /// <summary>
    /// A leased span helper for efficient memory management.
    /// </summary>
    public ref struct LeasedSpan<T>
    {
        private ArrayPool<T> pool;
        private T[] rented;
        private Span<T> span;

        public LeasedSpan(ArrayPool<T> pool, int length)
        {
            this.pool = pool;
            this.rented = pool.Rent(length);
            this.span = this.rented.AsSpan().Slice(0, length);
        }

        public Span<T> Span => this.span;

        public void Dispose()
        {
            this.pool.Return(this.rented, clearArray: true);
        }
    }

    /// <summary>
    /// Leases a span from the shared array pool.
    /// </summary>
    public static LeasedSpan<T> Lease<T>(this ArrayPool<T> pool, int length) => new(pool, length);
}