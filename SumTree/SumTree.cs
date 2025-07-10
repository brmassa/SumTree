using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SumTree;

/// <summary>
/// A SumTree is a high-performance data structure that directly incorporates the Rope tree structure
/// with integrated summary dimensions. It eliminates the composition overhead by directly implementing
/// the tree structure with integrated summary computation for maximum performance.
/// </summary>
/// <typeparam name="T">The type of elements, must be <see cref="IEquatable{T}"/>.</typeparam>
public readonly struct SumTree<T> : IEnumerable<T>, IReadOnlyList<T>, IEquatable<SumTree<T>>
    where T : IEquatable<T>
{
    /// <summary>
    /// Maximum tree depth allowed.
    /// </summary>
    private const int MaxTreeDepth = 46;

    /// <summary>
    /// Maximum depth discrepancy between left and right to cause a re-split of one side when balancing.
    /// </summary>
    private const int MaxDepthImbalance = 4;

    /// <summary>
    /// Maximum number of bytes before the GC puts buffers on the large object heap.
    /// </summary>
    private const int LargeObjectHeapBytes = 85_000 - 24;

    [ThreadStatic]
    private static int t_balancedCallDepth;

    /// <summary>
    /// Static size of the maximum length of a leaf node. This is calculated to never require Large Object Heap allocations.
    /// </summary>
    public static readonly int MaxLeafLength = CalculateMaxLeafLength();

    /// <summary>
    /// Defines the Empty leaf.
    /// </summary>
    public static readonly SumTree<T> Empty = new();

    /// <summary>
    /// Defines the minimum lengths the leaves should be in relation to the depth of the tree.
    /// </summary>
    private static readonly int[] s_fibonacciPlusTwo =
        Enumerable.Range(0, MaxTreeDepth).Select(d => Fibonacci(d) + 2).ToArray();

    private readonly object _data;
    private readonly Dictionary<Type, object>? _dimensions;
    private readonly Dictionary<Type, object>? _summaries;
    private readonly long _length;
    private readonly int _depth;
    private readonly int _bufferCount;
    private readonly bool _isBalanced;

    /// <summary>
    /// Creates a new empty SumTree.
    /// </summary>
    public SumTree()
    {
        _data = ReadOnlyMemory<T>.Empty;
        _isBalanced = true;
        _bufferCount = 1;
        _dimensions = null;
        _summaries = null;
        _length = 0;
        _depth = 0;
    }

    /// <summary>
    /// Creates a new SumTree with a single value.
    /// </summary>
    public SumTree(T value)
    {
        _data = ValueTuple.Create(value);
        _isBalanced = true;
        _length = 1;
        _bufferCount = 1;
        _dimensions = null;
        _summaries = null;
        _depth = 0;
    }

    /// <summary>
    /// Creates a new SumTree from memory with dimensions.
    /// </summary>
    public SumTree(ReadOnlyMemory<T> memory, Dictionary<Type, object>? dimensions = null)
    {
        if (memory.IsEmpty)
        {
            this = Empty;
            if (dimensions is { Count: > 0 })
            {
                _dimensions = new Dictionary<Type, object>(dimensions);
                _summaries = ComputeEmptySummaries(dimensions);
            }
            return;
        }

        _data = memory;
        _isBalanced = true;
        _length = memory.Length;
        _bufferCount = 1;
        _dimensions = dimensions?.Count > 0 ? new Dictionary<Type, object>(dimensions) : null;
        _summaries = _dimensions != null ? ComputeSummariesForMemory(memory, _dimensions) : null;
        _depth = 0;
    }

    /// <summary>
    /// Internal constructor for node creation.
    /// </summary>
    private SumTree(
        object data,
        long length,
        int depth,
        int bufferCount,
        bool isBalanced,
        Dictionary<Type, object>? dimensions = null,
        Dictionary<Type, object>? summaries = null)
    {
        _data = data;
        _length = length;
        _depth = depth;
        _bufferCount = bufferCount;
        _isBalanced = isBalanced;
        _dimensions = dimensions;
        _summaries = summaries;
    }

    /// <summary>
    /// Creates a new SumTree by combining two existing SumTree instances.
    /// This constructor provides an alternative to using the + operator for combining trees.
    /// </summary>
    /// <param name="left">The left SumTree to combine</param>
    /// <param name="right">The right SumTree to combine</param>
    /// <example>
    /// <code>
    /// var left = "Hello".ToSumTree();
    /// var right = " World".ToSumTree();
    /// var combined = new SumTree&lt;char&gt;(left, right);
    /// Console.WriteLine(combined); // Outputs: Hello World
    /// </code>
    /// </example>
    /// <remarks>
    /// This constructor is equivalent to using the + operator: left + right.
    /// The resulting tree maintains all dimensions and summaries from both input trees.
    /// </remarks>
    public SumTree(SumTree<T> left, SumTree<T> right)
    {
        var result = Concat(left, right);
        _data = result._data;
        _length = result._length;
        _depth = result._depth;
        _bufferCount = result._bufferCount;
        _isBalanced = result._isBalanced;
        _dimensions = result._dimensions;
        _summaries = result._summaries;
    }

    /// <summary>
    /// Gets the number of buffers (leaf nodes) in this tree.
    /// </summary>
    public int BufferCount => _bufferCount;

    /// <summary>
    /// Gets the left child if this is a node.
    /// </summary>
    internal SumTree<T> Left => IsNode ? ((ValueTuple<SumTree<T>, SumTree<T>>)_data).Item1 : Empty;

    /// <summary>
    /// Gets the right child if this is a node.
    /// </summary>
    internal SumTree<T> Right => IsNode ? ((ValueTuple<SumTree<T>, SumTree<T>>)_data).Item2 : Empty;

    /// <summary>
    /// Gets whether this is a node (has children) rather than a leaf.
    /// </summary>
    public bool IsNode => _data is ValueTuple<SumTree<T>, SumTree<T>>;

    /// <summary>
    /// Gets the weight (length of left subtree) for balancing.
    /// </summary>
    public long Weight => IsNode ? Left._length : 0;

    /// <summary>
    /// Gets the total length of elements in this tree.
    /// </summary>
    public long Length => _length;

    /// <summary>
    /// Gets whether this tree is empty.
    /// </summary>
    public bool IsEmpty => _length == 0;

    /// <summary>
    /// Gets the depth of this tree.
    /// </summary>
    public int Depth => _depth;

    /// <summary>
    /// Gets whether this tree is balanced.
    /// </summary>
    public bool IsBalanced => _isBalanced;

    /// <summary>
    /// Gets the number of registered summary dimensions.
    /// </summary>
    public int DimensionCount => _dimensions?.Count ?? 0;

    /// <summary>
    /// Gets the number of elements (same as Length but as int for IReadOnlyList).
    /// </summary>
    public int Count => (int)Math.Min(_length, int.MaxValue);

    /// <summary>
    /// Gets the dimensions dictionary (for internal use by extensions).
    /// </summary>
    internal Dictionary<Type, object>? GetDimensions() => _dimensions;

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ElementAt(long index) => this[index];

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    public T this[long index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (index < 0 || index >= _length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return GetElementAtIndex(index);
        }
    }

    /// <summary>
    /// Gets the element at the specified index (IReadOnlyList implementation).
    /// </summary>
    public T this[int index] => this[(long)index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T GetElementAtIndex(long index)
    {
        var current = this;
        var currentIndex = index;

        while (current.IsNode)
        {
            var leftLength = current.Left._length;
            if (currentIndex < leftLength)
            {
                current = current.Left;
            }
            else
            {
                current = current.Right;
                currentIndex -= leftLength;
            }
        }

        return current._data switch
        {
            ReadOnlyMemory<T> memory => memory.Span[(int)currentIndex],
            ValueTuple<T> single => single.Item1,
            _ => throw new InvalidOperationException("Invalid data type")
        };
    }

    /// <summary>
    /// Adds a summary dimension to this SumTree.
    /// </summary>
    public SumTree<T> WithDimension<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        var newDimensions = new Dictionary<Type, object>(_dimensions ?? new Dictionary<Type, object>());
        newDimensions[typeof(TSummary)] = dimension;

        var newSummaries = new Dictionary<Type, object>(_summaries ?? new Dictionary<Type, object>());
        newSummaries[typeof(TSummary)] = ComputeSummaryForTree(dimension);

        return new SumTree<T>(_data, _length, _depth, _bufferCount, _isBalanced, newDimensions, newSummaries);
    }

    /// <summary>
    /// Gets the summary for a specific dimension type.
    /// </summary>
    public TSummary GetSummary<TSummary>() where TSummary : IEquatable<TSummary>
    {
        if (_summaries == null || !_summaries.TryGetValue(typeof(TSummary), out var summary))
            throw new InvalidOperationException(
                $"Dimension for summary type {typeof(TSummary).Name} is not registered.");

        return (TSummary)summary;
    }

    /// <summary>
    /// Tries to get the summary for a specific dimension type.
    /// </summary>
    public bool TryGetSummary<TSummary>(out TSummary summary) where TSummary : IEquatable<TSummary>
    {
        summary = default!;
        if (_summaries == null || !_summaries.TryGetValue(typeof(TSummary), out var summaryObj))
            return false;

        summary = (TSummary)summaryObj;
        return true;
    }

    /// <summary>
    /// Checks if a dimension is registered for the specified summary type.
    /// </summary>
    public bool HasDimension<TSummary>() where TSummary : IEquatable<TSummary>
    {
        return _dimensions != null && _dimensions.ContainsKey(typeof(TSummary));
    }

    /// <summary>
    /// Splits the tree at the specified index.
    /// </summary>
    public (SumTree<T> Left, SumTree<T> Right) SplitAt(long index)
    {
        if (index <= 0)
            return (Empty.WithDimensions(_dimensions), this);
        if (index >= _length)
            return (this, Empty.WithDimensions(_dimensions));

        return SplitAtInternal(index);
    }

    /// <summary>
    /// Inserts an element at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> Insert(long index, T item)
    {
        var itemTree = new SumTree<T>(item);
        return InsertRange(index, itemTree);
    }

    /// <summary>
    /// Inserts a range of elements at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> InsertRange(long index, ReadOnlyMemory<T> items)
    {
        if (items.IsEmpty)
            return this;

        var itemTree = new SumTree<T>(items, _dimensions);
        return InsertRange(index, itemTree);
    }

    /// <summary>
    /// Inserts another SumTree at the specified index.
    /// </summary>
    public SumTree<T> InsertRange(long index, SumTree<T> other)
    {
        if (other.IsEmpty)
            return this;

        if (index == 0) return other.WithDimensions(_dimensions).AddRange(this);
        if (index == _length) return AddRange(other);

        var (left, right) = SplitAt(index);
        return Concat(Concat(left, other.WithDimensions(_dimensions)), right);
    }

    /// <summary>
    /// Removes a range of elements.
    /// </summary>
    public SumTree<T> RemoveRange(long start, long length)
    {
        if (length <= 0 || start >= _length)
            return this;

        var end = Math.Min(start + length, _length);
        var (left, temp) = SplitAt(start);
        var (_, right) = temp.SplitAt(end - start);
        return Concat(left, right);
    }

    /// <summary>
    /// Removes an element at the specified index.
    /// </summary>
    public SumTree<T> RemoveAt(long index)
    {
        return RemoveRange(index, 1);
    }

    /// <summary>
    /// Gets a slice of the tree.
    /// </summary>
    public SumTree<T> Slice(long start, long length)
    {
        if (length <= 0 || start >= _length)
            return Empty.WithDimensions(_dimensions);

        var end = Math.Min(start + length, _length);
        var (_, temp) = SplitAt(start);
        var (result, _) = temp.SplitAt(end - start);
        return result;
    }

    /// <summary>
    /// Gets a slice from the starting index to the end.
    /// </summary>
    public SumTree<T> Slice(long start)
    {
        return Slice(start, _length - start);
    }

    /// <summary>
    /// Appends an element to the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> Add(T item)
    {
        var itemTree = new SumTree<T>(item);
        return AddRange(itemTree);
    }

    /// <summary>
    /// Appends another SumTree to the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> AddRange(SumTree<T> other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        return Concat(this, other);
    }

    /// <summary>
    /// Appends a memory range to the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> AddRange(ReadOnlyMemory<T> items)
    {
        if (items.IsEmpty) return this;
        return AddRange(new SumTree<T>(items, _dimensions));
    }

    /// <summary>
    /// Appends an array to the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> AddRange(T[] items)
    {
        return AddRange(items.AsMemory());
    }

    /// <summary>
    /// Appends an enumerable to the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> AddRange(IEnumerable<T> items)
    {
        if (items is T[] array)
            return AddRange(array);

        if (items is List<T> list)
            return AddRange(CollectionsMarshal.AsSpan(list));

        return AddRange(items.ToArray());
    }

    /// <summary>
    /// Appends a span to the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> AddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty) return this;
        // Need to copy span to memory since span is stack-only
        var array = items.ToArray();
        return AddRange(new SumTree<T>(array.AsMemory(), _dimensions));
    }

    /// <summary>
    /// Finds the position where a condition based on a summary becomes true.
    /// </summary>
    public long FindPosition<TSummary>(Func<TSummary, bool> predicate) where TSummary : IEquatable<TSummary>
    {
        if (_dimensions == null || !_dimensions.TryGetValue(typeof(TSummary), out var dimensionObj))
            throw new InvalidOperationException(
                $"Dimension for summary type {typeof(TSummary).Name} is not registered.");

        var dimension = (ISummaryDimension<T, TSummary>)dimensionObj;
        return FindPositionRecursive(dimension, predicate, dimension.Identity, 0);
    }

    /// <summary>
    /// Concatenates two trees.
    /// </summary>
    public static SumTree<T> operator +(SumTree<T> left, SumTree<T> right)
    {
        return Concat(left, right);
    }

    /// <summary>
    /// Gets an enumerator for the elements.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return EnumerateElements().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<T> EnumerateElements()
    {
        if (IsEmpty) yield break;

        if (!IsNode)
        {
            switch (_data)
            {
                case ReadOnlyMemory<T> memory:
                    var array = memory.ToArray();
                    for (var i = 0; i < array.Length; i++)
                        yield return array[i];
                    break;
                case ValueTuple<T> single:
                    yield return single.Item1;
                    break;
            }
            yield break;
        }

        foreach (var item in Left.EnumerateElements())
            yield return item;
        foreach (var item in Right.EnumerateElements())
            yield return item;
    }

    /// <summary>
    /// Converts to array efficiently
    /// </summary>
    public T[] ToArray()
    {
        if (IsEmpty) return Array.Empty<T>();

        var result = new T[_length];
        var index = 0;
        CopyTo(result.AsSpan(), ref index);
        return result;
    }

    /// <summary>
    /// Copies elements to a span efficiently
    /// </summary>
    private void CopyTo(Span<T> destination, ref int index)
    {
        if (IsEmpty) return;

        if (!IsNode)
        {
            switch (_data)
            {
                case ReadOnlyMemory<T> memory:
                    memory.Span.CopyTo(destination.Slice(index));
                    index += memory.Length;
                    break;
                case ValueTuple<T> single:
                    destination[index++] = single.Item1;
                    break;
            }
            return;
        }

        Left.CopyTo(destination, ref index);
        Right.CopyTo(destination, ref index);
    }

    /// <summary>
    /// Converts to string (for char trees)
    /// </summary>
    public string ToStringFast()
    {
        if (typeof(T) != typeof(char))
            throw new InvalidOperationException("ToStringFast only works with char trees");

        if (IsEmpty) return string.Empty;

        if (_length <= 1024)
        {
            Span<char> buffer = stackalloc char[(int)_length];
            var index = 0;
            CopyToChar(buffer, ref index);
            return new string(buffer);
        }

        var sb = new System.Text.StringBuilder((int)_length);
        AppendToStringBuilder(sb);
        return sb.ToString();
    }

    private void CopyToChar(Span<char> destination, ref int index)
    {
        if (typeof(T) != typeof(char)) return;

        if (IsEmpty) return;

        if (!IsNode)
        {
            switch (_data)
            {
                case ReadOnlyMemory<char> memory:
                    memory.Span.CopyTo(destination.Slice(index));
                    index += memory.Length;
                    break;
                case ValueTuple<char> single:
                    destination[index++] = single.Item1;
                    break;
            }
            return;
        }

        ((SumTree<char>)(object)Left).CopyToChar(destination, ref index);
        ((SumTree<char>)(object)Right).CopyToChar(destination, ref index);
    }

    private void AppendToStringBuilder(System.Text.StringBuilder sb)
    {
        if (typeof(T) != typeof(char)) return;

        if (IsEmpty) return;

        if (!IsNode)
        {
            switch (_data)
            {
                case ReadOnlyMemory<char> memory:
                    sb.Append(memory.Span);
                    break;
                case ValueTuple<char> single:
                    sb.Append(single.Item1);
                    break;
            }
            return;
        }

        ((SumTree<char>)(object)Left).AppendToStringBuilder(sb);
        ((SumTree<char>)(object)Right).AppendToStringBuilder(sb);
    }

    /// <summary>
    /// Determines whether the specified SumTree is equal to the current instance.
    /// </summary>
    public bool Equals(SumTree<T> other)
    {
        if (_length != other._length) return false;
        if (_length == 0) return true;

        var thisEnum = EnumerateElements();
        var otherEnum = other.EnumerateElements();

        return thisEnum.SequenceEqual(otherEnum);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is SumTree<T> other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code for the current instance.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_length);

        foreach (var item in EnumerateElements())
            hash.Add(item);

        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns a string representation of the SumTree.
    /// </summary>
    public override string ToString()
    {
        // For SumTree<char>, return the actual string content
        if (typeof(T) == typeof(char))
        {
            var array = new T[_length];
            var index = 0;
            foreach (var item in this)
            {
                array[index++] = item;
            }
            return new string((char[])(object)array);
        }

        return $"SumTree<{typeof(T).Name}>[Length={_length}, Depth={_depth}, BufferCount={_bufferCount}]";
    }

    public static bool operator ==(SumTree<T> left, SumTree<T> right) => left.Equals(right);
    public static bool operator !=(SumTree<T> left, SumTree<T> right) => !left.Equals(right);

    // Private implementation methods

    private (SumTree<T> Left, SumTree<T> Right) SplitAtInternal(long index)
    {
        var current = this;
        var currentIndex = index;
        var leftParts = new List<SumTree<T>>();
        var rightParts = new List<SumTree<T>>();

        while (current.IsNode)
        {
            var leftLength = current.Left._length;
            if (currentIndex <= leftLength)
            {
                // Split will happen in left subtree
                rightParts.Add(current.Right);
                current = current.Left;
            }
            else
            {
                // Split will happen in right subtree
                leftParts.Add(current.Left);
                current = current.Right;
                currentIndex -= leftLength;
            }
        }

        // Now we have a leaf node, split it
        var (leafLeft, leafRight) = current._data switch
        {
            ReadOnlyMemory<T> memory => SplitMemory(memory, (int)currentIndex),
            ValueTuple<T> single => currentIndex == 0
                ? (Empty.WithDimensions(_dimensions), current)
                : (current, Empty.WithDimensions(_dimensions)),
            _ => throw new InvalidOperationException("Invalid data type")
        };

        // Combine left parts with leaf left
        var left = leafLeft;
        for (var i = leftParts.Count - 1; i >= 0; i--)
        {
            left = Concat(leftParts[i], left);
        }

        // Combine leaf right with right parts
        var right = leafRight;
        for (var i = rightParts.Count - 1; i >= 0; i--)
        {
            right = Concat(right, rightParts[i]);
        }

        return (left, right);
    }

    private (SumTree<T> Left, SumTree<T> Right) SplitMemory(ReadOnlyMemory<T> memory, int index)
    {
        if (index == 0) return (Empty.WithDimensions(_dimensions), new SumTree<T>(memory, _dimensions));
        if (index >= memory.Length) return (new SumTree<T>(memory, _dimensions), Empty.WithDimensions(_dimensions));

        var leftMemory = memory[..index];
        var rightMemory = memory[index..];

        return (
            new SumTree<T>(leftMemory, _dimensions),
            new SumTree<T>(rightMemory, _dimensions)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SumTree<T> Concat(SumTree<T> left, SumTree<T> right)
    {
        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;

        var newLength = left._length + right._length;
        var newDepth = Math.Max(left._depth, right._depth) + 1;
        var newBufferCount = left._bufferCount + right._bufferCount;
        var isBalanced = CalculateIsBalanced(newLength, newDepth);

        // Merge dimensions
        var dimensions = MergeDimensions(left._dimensions, right._dimensions);
        var summaries = dimensions != null ? ComputeCombinedSummaries(left, right, dimensions) : null;

        var result = new SumTree<T>(
            new ValueTuple<SumTree<T>, SumTree<T>>(left, right),
            newLength,
            newDepth,
            newBufferCount,
            isBalanced,
            dimensions,
            summaries
        );

        return isBalanced ? result : result.Balanced();
    }

    private SumTree<T> Balanced()
    {
        if (_isBalanced) return this;

        t_balancedCallDepth++;
        try
        {
            if (t_balancedCallDepth > MaxTreeDepth)
                throw new InvalidOperationException("Tree depth exceeded maximum");

            var leaves = new List<SumTree<T>>();
            CollectLeaves(leaves);
            return CombineLeaves(leaves);
        }
        finally
        {
            t_balancedCallDepth--;
        }
    }

    private void CollectLeaves(List<SumTree<T>> leaves)
    {
        if (!IsNode)
        {
            leaves.Add(this);
            return;
        }

        Left.CollectLeaves(leaves);
        Right.CollectLeaves(leaves);
    }

    private static SumTree<T> CombineLeaves(List<SumTree<T>> leaves)
    {
        while (leaves.Count > 1)
        {
            var newLeaves = new List<SumTree<T>>();

            for (var i = 0; i < leaves.Count; i += 2)
            {
                if (i + 1 < leaves.Count)
                {
                    newLeaves.Add(ConcatDirect(leaves[i], leaves[i + 1]));
                }
                else
                {
                    newLeaves.Add(leaves[i]);
                }
            }

            leaves = newLeaves;
        }

        return leaves.Count == 1 ? leaves[0] : Empty;
    }

    private static SumTree<T> ConcatDirect(SumTree<T> left, SumTree<T> right)
    {
        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;

        var newLength = left._length + right._length;
        var newDepth = Math.Max(left._depth, right._depth) + 1;
        var newBufferCount = left._bufferCount + right._bufferCount;
        var isBalanced = CalculateIsBalanced(newLength, newDepth);

        // Merge dimensions
        var dimensions = MergeDimensions(left._dimensions, right._dimensions);
        var summaries = dimensions != null ? ComputeCombinedSummaries(left, right, dimensions) : null;

        return new SumTree<T>(
            new ValueTuple<SumTree<T>, SumTree<T>>(left, right),
            newLength,
            newDepth,
            newBufferCount,
            isBalanced,
            dimensions,
            summaries
        );
    }

    private SumTree<T> WithDimensions(Dictionary<Type, object>? newDimensions)
    {
        if (newDimensions == null || newDimensions.Count == 0)
            return this;

        if (_dimensions != null && DimensionsEqual(_dimensions, newDimensions))
            return this;

        var newSummaries = ComputeAllSummaries(newDimensions);
        return new SumTree<T>(_data, _length, _depth, _bufferCount, _isBalanced, newDimensions, newSummaries);
    }

    private long FindPositionRecursive<TSummary>(
        ISummaryDimension<T, TSummary> dimension,
        Func<TSummary, bool> predicate,
        TSummary currentSummary,
        long currentIndex) where TSummary : IEquatable<TSummary>
    {
        if (IsEmpty) return -1;

        if (IsNode)
        {
            // Try left subtree first
            var leftResult = Left.FindPositionRecursive(dimension, predicate, currentSummary, currentIndex);
            if (leftResult >= 0) return leftResult;

            // If not found in left, try right with accumulated summary
            var leftSummary = Left.GetSummaryForDimension(dimension);
            var combinedSummary = dimension.Combine(currentSummary, leftSummary);
            return Right.FindPositionRecursive(dimension, predicate, combinedSummary, currentIndex + Left._length);
        }

        // Leaf node - check each element
        var summary = currentSummary;
        var index = 0L;

        foreach (var element in EnumerateElements())
        {
            var elementSummary = dimension.SummarizeElement(element);
            var newSummary = dimension.Combine(summary, elementSummary);

            if (predicate(newSummary))
                return currentIndex + index;

            summary = newSummary;
            index++;
        }

        return -1;
    }

    /// <summary>
    /// Gets the summary for the specified dimension.
    /// </summary>
    /// <typeparam name="TSummary">The type of the summary.</typeparam>
    /// <param name="dimension">The dimension to compute the summary for.</param>
    /// <returns>The summary for the dimension.</returns>
    public TSummary GetSummary<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        if (_summaries != null && _summaries.TryGetValue(typeof(TSummary), out var summary))
            return (TSummary)summary;

        return ComputeSummaryForTree(dimension);
    }

    private TSummary GetSummaryForDimension<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        return GetSummary(dimension);
    }

    private TSummary ComputeSummaryForTree<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        if (IsEmpty) return dimension.Identity;

        if (IsNode)
        {
            var leftSummary = Left.ComputeSummaryForTree(dimension);
            var rightSummary = Right.ComputeSummaryForTree(dimension);
            return dimension.Combine(leftSummary, rightSummary);
        }

        // Leaf node
        return _data switch
        {
            ReadOnlyMemory<T> memory => dimension.SummarizeSpan(memory.Span),
            ValueTuple<T> single => dimension.SummarizeElement(single.Item1),
            _ => dimension.Identity
        };
    }

    // Static helper methods

    private static Dictionary<Type, object>? MergeDimensions(
        Dictionary<Type, object>? left,
        Dictionary<Type, object>? right)
    {
        if (left == null) return right;
        if (right == null) return left;

        var result = new Dictionary<Type, object>(left);
        foreach (var kvp in right)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    private static Dictionary<Type, object>? ComputeCombinedSummaries(
        SumTree<T> left,
        SumTree<T> right,
        Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>();

        foreach (var kvp in dimensions)
        {
            var summaryType = kvp.Key;
            var dimension = kvp.Value;

            var leftSummary = left._summaries?.GetValueOrDefault(summaryType);
            var rightSummary = right._summaries?.GetValueOrDefault(summaryType);

            if (leftSummary != null && rightSummary != null)
            {
                // Combine existing summaries
                var combineMethod = dimension.GetType().GetMethod("Combine");
                result[summaryType] = combineMethod!.Invoke(dimension, [leftSummary, rightSummary])!;
            }
            else
            {
                // Recompute summary
                result[summaryType] = ComputeSummaryForDimensionStatic(left, right, dimension);
            }
        }

        return result;
    }

    private static object ComputeSummaryForDimensionStatic(SumTree<T> left, SumTree<T> right, object dimension)
    {
        var dimensionType = dimension.GetType();
        var interfaces = dimensionType.GetInterfaces();
        var summaryInterface = interfaces.FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>));

        if (summaryInterface == null)
            throw new ArgumentException($"Invalid dimension type: {dimensionType}");

        var summaryType = summaryInterface.GetGenericArguments()[1];
        var combineMethod = dimensionType.GetMethod("Combine");
        var identityProperty = dimensionType.GetProperty("Identity");

        var leftSummary = left._summaries?.GetValueOrDefault(summaryType) ?? identityProperty!.GetValue(dimension);
        var rightSummary = right._summaries?.GetValueOrDefault(summaryType) ?? identityProperty!.GetValue(dimension);

        return combineMethod!.Invoke(dimension, [leftSummary, rightSummary])!;
    }

    private Dictionary<Type, object> ComputeAllSummaries(Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>();

        foreach (var kvp in dimensions)
        {
            var summaryType = kvp.Key;
            var dimension = kvp.Value;
            result[summaryType] = ComputeSummaryForDimensionInstance(dimension);
        }

        return result;
    }

    private object ComputeSummaryForDimensionInstance(object dimension)
    {
        var dimensionType = dimension.GetType();
        var interfaces = dimensionType.GetInterfaces();
        var summaryInterface = interfaces.FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>));

        if (summaryInterface == null)
            throw new ArgumentException($"Invalid dimension type: {dimensionType}");

        var method = typeof(SumTree<T>).GetMethod(nameof(ComputeSummaryForTree),
            BindingFlags.NonPublic | BindingFlags.Instance);
        var summaryType = summaryInterface.GetGenericArguments()[1];
        var genericMethod = method!.MakeGenericMethod(summaryType);

        return genericMethod.Invoke(this, [dimension])!;
    }

    private static Dictionary<Type, object> ComputeEmptySummaries(Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>();

        foreach (var kvp in dimensions)
        {
            var dimension = kvp.Value;
            var identityProperty = dimension.GetType().GetProperty("Identity");
            result[kvp.Key] = identityProperty!.GetValue(dimension)!;
        }

        return result;
    }

    private static Dictionary<Type, object> ComputeSummariesForMemory(ReadOnlyMemory<T> memory,
        Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>();

        foreach (var kvp in dimensions)
        {
            var summaryType = kvp.Key;
            var dimension = kvp.Value;

            // Use reflection to call the generic SummarizeSpan method
            var dimensionType = dimension.GetType();
            var interfaces = dimensionType.GetInterfaces();
            var summaryInterface = interfaces.FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>));

            if (summaryInterface != null)
            {
                var method = typeof(SumTree<T>).GetMethod(nameof(ComputeSummaryForMemoryGeneric),
                    BindingFlags.NonPublic | BindingFlags.Static);
                var genericMethod = method!.MakeGenericMethod(summaryInterface.GetGenericArguments()[1]);
                result[summaryType] = genericMethod.Invoke(null, [memory, dimension])!;
            }
        }

        return result;
    }

    private static TSummary ComputeSummaryForMemoryGeneric<TSummary>(ReadOnlyMemory<T> memory,
        ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        return dimension.SummarizeSpan(memory.Span);
    }

    private static bool DimensionsEqual(Dictionary<Type, object>? left, Dictionary<Type, object>? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;
        if (left.Count != right.Count) return false;

        foreach (var kvp in left)
        {
            if (!right.TryGetValue(kvp.Key, out var rightValue) || !kvp.Value.Equals(rightValue))
                return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CalculateIsBalanced(long length, int depth)
    {
        return depth < MaxTreeDepth && (depth == 0 || length >= s_fibonacciPlusTwo[depth]);
    }

    private static int Fibonacci(int n)
    {
        if (n <= 1) return n;

        int a = 0, b = 1;
        for (var i = 2; i <= n; i++)
        {
            var temp = a + b;
            a = b;
            b = temp;
        }
        return b;
    }

    private static int CalculateMaxLeafLength()
    {
        var elementSize = Unsafe.SizeOf<T>();
        var maxElements = LargeObjectHeapBytes / elementSize;
        const int cacheLineSize = 64;
        var alignedElements = (maxElements / cacheLineSize) * cacheLineSize;
        return Math.Max(alignedElements, cacheLineSize);
    }
}
