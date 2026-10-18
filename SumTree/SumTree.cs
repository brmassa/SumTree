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
/// <remarks>
/// Snapshots share immutable storage. Elements and summary values must be immutable,
/// and dimension implementations must be pure and safe for concurrent calls.
/// Buffer copies are shallow. Publish snapshots through <see cref="SumTreeSnapshotStore{T}"/>
/// or external synchronization rather than concurrently assigning this multi-field struct.
/// </remarks>
/// <typeparam name="T">The type of elements, must be <see cref="IEquatable{T}"/>.</typeparam>
public readonly struct SumTree<T> : IEnumerable<T>, IReadOnlyList<T>, IEquatable<SumTree<T>>
    where T : IEquatable<T>
{
    /// <summary>
    /// Maximum tree depth allowed.
    /// </summary>
    const int MaxTreeDepth = 46;

    /// <summary>
    /// Maximum depth discrepancy between left and right to cause a re-split of one side when balancing.
    /// </summary>
    const int MaxDepthImbalance = 4;

    /// <summary>
    /// Maximum number of bytes before the GC puts buffers on the large object heap.
    /// </summary>
    const int LargeObjectHeapBytes = 85_000 - 24;

    [ThreadStatic]
    static int t_balancedCallDepth;

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
    static readonly int[] s_fibonacciPlusTwo =
        [.. Enumerable.Range(0, MaxTreeDepth).Select(d => Fibonacci(d) + 2)];

    readonly object _data;
    readonly Dictionary<Type, object>? _dimensions;
    readonly Dictionary<Type, object>? _summaries;
    readonly long _length;
    readonly int _depth;
    readonly int _bufferCount;
    readonly bool _isBalanced;

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
    /// Creates a new SumTree, copying mutable memory and the dimension dictionary.
    /// String-backed character memory is shared without copying.
    /// </summary>
    public SumTree(ReadOnlyMemory<T> memory, Dictionary<Type, object>? dimensions = null)
        : this(SnapshotMemory.Copy(memory), dimensions, copyDimensions: true)
    {
    }

    SumTree(ReadOnlyMemory<T> memory, Dictionary<Type, object>? dimensions, bool copyDimensions)
    {
        if (copyDimensions && dimensions is { Count: > 0 })
        {
            dimensions = new Dictionary<Type, object>(dimensions);
        }

        if (memory.IsEmpty)
        {
            this = Empty;
            if (dimensions is { Count: > 0 })
            {
                _dimensions = dimensions;
                _summaries = ComputeEmptySummaries(dimensions);
            }
            return;
        }

        if (memory.Length > ChunkLength)
        {
            this = Chunked(memory, dimensions?.Count > 0 ? dimensions : null);
            return;
        }

        _data = memory;
        _isBalanced = true;
        _length = memory.Length;
        _bufferCount = 1;
        _dimensions = dimensions?.Count > 0 ? dimensions : null;
        _summaries = _dimensions != null ? ComputeSummariesForMemory(memory, _dimensions) : null;
        _depth = 0;
    }

    /// <summary>Creates a tree without copying its backing memory.</summary>
    /// <remarks>
    /// The caller must keep the memory valid and unchanged for the lifetime of this tree
    /// and every derived snapshot. Do not return its array to a pool or dispose its owner.
    /// The dimension dictionary is copied; dimension instances and elements must be immutable.
    /// </remarks>
    public static SumTree<T> FromBorrowedMemory(ReadOnlyMemory<T> memory,
        Dictionary<Type, object>? dimensions = null) => new(memory, dimensions, copyDimensions: true);

    /// <summary>Shares storage already owned by the library and immutable dimension configuration.</summary>
    internal static SumTree<T> FromImmutableMemory(ReadOnlyMemory<T> memory,
        Dictionary<Type, object>? dimensions = null) => new(memory, dimensions, copyDimensions: false);

    /// <summary>
    /// Internal constructor for node creation.
    /// </summary>
    SumTree(
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
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetElementAtIndex(index);
        }
    }

    /// <summary>
    /// Gets the element at the specified index (IReadOnlyList implementation).
    /// </summary>
    public T this[int index] => this[(long)index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    T GetElementAtIndex(long index)
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

        return WithDimensions(newDimensions);
    }

    /// <summary>
    /// Gets the summary for a specific dimension type.
    /// </summary>
    public TSummary GetSummary<TSummary>() where TSummary : IEquatable<TSummary>
    {
        if (_summaries == null || !_summaries.TryGetValue(typeof(TSummary), out var summary))
        {
            throw new InvalidOperationException(
                $"Dimension for summary type {typeof(TSummary).Name} is not registered.");
        }

        return (TSummary)summary;
    }

    /// <summary>
    /// Tries to get the summary for a specific dimension type.
    /// </summary>
    public bool TryGetSummary<TSummary>(out TSummary summary) where TSummary : IEquatable<TSummary>
    {
        summary = default!;
        if (_summaries == null || !_summaries.TryGetValue(typeof(TSummary), out var summaryObj))
        {
            return false;
        }

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
        {
            return (Empty.WithDimensions(_dimensions), this);
        }

        if (index >= _length)
        {
            return (this, Empty.WithDimensions(_dimensions));
        }

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
        {
            return this;
        }

        var itemTree = FromImmutableMemory(SnapshotMemory.Copy(items), _dimensions);
        return InsertRange(index, itemTree);
    }

    /// <summary>
    /// Inserts another SumTree at the specified index.
    /// </summary>
    public SumTree<T> InsertRange(long index, SumTree<T> other)
    {
        if (other.IsEmpty)
        {
            return this;
        }

        if (index == 0)
        {
            return other.WithDimensions(_dimensions).AddRange(this);
        }

        if (index == _length)
        {
            return AddRange(other);
        }

        var (left, right) = SplitAt(index);
        return Concat(Concat(left, other.WithDimensions(_dimensions)), right);
    }

    /// <summary>
    /// Removes a range of elements.
    /// </summary>
    public SumTree<T> RemoveRange(long start, long length)
    {
        if (length <= 0 || start >= _length)
        {
            return this;
        }

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
        {
            return Empty.WithDimensions(_dimensions);
        }

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
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return Concat(this, other);
    }

    /// <summary>
    /// Appends a memory range to the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> AddRange(ReadOnlyMemory<T> items)
    {
        if (items.IsEmpty)
        {
            return this;
        }

        return AddRange(FromImmutableMemory(SnapshotMemory.Copy(items), _dimensions));
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
        {
            return AddRange(array);
        }

        if (items is List<T> list)
        {
            return AddRange(CollectionsMarshal.AsSpan(list));
        }

        return AddRange(FromImmutableMemory(items.ToArray(), _dimensions));
    }

    /// <summary>
    /// Appends a span to the end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SumTree<T> AddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty)
        {
            return this;
        }
        // Need to copy span to memory since span is stack-only
        var array = items.ToArray();
        return AddRange(FromImmutableMemory(array.AsMemory(), _dimensions));
    }

    /// <summary>
    /// Finds the first index whose running summary, including that element, satisfies the predicate.
    /// The predicate must be monotonic (once true for a prefix, true for every longer prefix), which lets the
    /// search descend the tree in O(log n) using the cached summaries instead of visiting every element.
    /// </summary>
    /// <returns>The index, or -1 when no prefix satisfies the predicate.</returns>
    public long FindPosition<TSummary>(Func<TSummary, bool> predicate) where TSummary : IEquatable<TSummary>
    {
        if (_dimensions == null || !_dimensions.TryGetValue(typeof(TSummary), out var dimensionObj))
        {
            throw new InvalidOperationException(
                $"Dimension for summary type {typeof(TSummary).Name} is not registered.");
        }

        var dimension = (ISummaryDimension<T, TSummary>)dimensionObj;
        if (IsEmpty || !predicate(GetSummary(dimension)))
        {
            return -1;
        }

        var current = this;
        var summary = dimension.Identity;
        var offset = 0L;
        while (current.IsNode)
        {
            var left = current.Left;
            var combined = dimension.Combine(summary, left.GetSummary(dimension));
            if (predicate(combined))
            {
                current = left;
                continue;
            }

            summary = combined;
            offset += left._length;
            current = current.Right;
        }

        return current.FindPositionInLeaf(dimension, predicate, summary, offset);
    }

    /// <summary>
    /// Concatenates two trees.
    /// </summary>
    public static SumTree<T> operator +(SumTree<T> left, SumTree<T> right) => Concat(left, right);

    /// <summary>
    /// Gets an enumerator for the elements.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return EnumerateElements().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IEnumerable<T> EnumerateElements()
    {
        if (IsEmpty)
        {
            yield break;
        }

        if (!IsNode)
        {
            switch (_data)
            {
                case ReadOnlyMemory<T> memory:
                    for (var i = 0; i < memory.Length; i++)
                    {
                        yield return memory.Span[i];
                    }

                    break;
                case ValueTuple<T> single:
                    yield return single.Item1;
                    break;
            }
            yield break;
        }

        foreach (var item in Left.EnumerateElements())
        {
            yield return item;
        }

        foreach (var item in Right.EnumerateElements())
        {
            yield return item;
        }
    }

    /// <summary>
    /// Converts to array efficiently
    /// </summary>
    public T[] ToArray()
    {
        if (IsEmpty)
        {
            return [];
        }

        var result = new T[_length];
        var index = 0;
        CopyTo(result.AsSpan(), ref index);
        return result;
    }

    /// <summary>
    /// Copies elements to a span efficiently
    /// </summary>
    void CopyTo(Span<T> destination, ref int index)
    {
        if (IsEmpty)
        {
            return;
        }

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
        {
            throw new InvalidOperationException("ToStringFast only works with char trees");
        }

        if (IsEmpty)
        {
            return string.Empty;
        }

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

    void CopyToChar(Span<char> destination, ref int index)
    {
        if (typeof(T) != typeof(char))
        {
            return;
        }

        if (IsEmpty)
        {
            return;
        }

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

    void AppendToStringBuilder(System.Text.StringBuilder sb)
    {
        if (typeof(T) != typeof(char))
        {
            return;
        }

        if (IsEmpty)
        {
            return;
        }

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
        if (_length != other._length)
        {
            return false;
        }

        if (_length == 0)
        {
            return true;
        }

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
        {
            hash.Add(item);
        }

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
            return ToStringFast();
        }

        return $"SumTree<{typeof(T).Name}>[Length={_length}, Depth={_depth}, BufferCount={_bufferCount}]";
    }

    public static bool operator ==(SumTree<T> left, SumTree<T> right) => left.Equals(right);
    public static bool operator !=(SumTree<T> left, SumTree<T> right) => !left.Equals(right);

    // Private implementation methods

    (SumTree<T> Left, SumTree<T> Right) SplitAtInternal(long index)
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

    (SumTree<T> Left, SumTree<T> Right) SplitMemory(ReadOnlyMemory<T> memory, int index)
    {
        if (index == 0)
        {
            return (Empty.WithDimensions(_dimensions), FromImmutableMemory(memory, _dimensions));
        }

        if (index >= memory.Length)
        {
            return (FromImmutableMemory(memory, _dimensions), Empty.WithDimensions(_dimensions));
        }

        var leftMemory = memory[..index];
        var rightMemory = memory[index..];

        return (
            FromImmutableMemory(leftMemory, _dimensions),
            FromImmutableMemory(rightMemory, _dimensions)
        );
    }

    /// <summary>
    /// Longest leaf built from a single buffer. Longer buffers become a balanced tree of slices, so a search that
    /// ends in a leaf scans at most this many elements.
    /// </summary>
    const int ChunkLength = 1024;

    /// <summary>A balanced tree over slices of one buffer, sharing its memory and one dimension set.</summary>
    static SumTree<T> Chunked(ReadOnlyMemory<T> memory, Dictionary<Type, object>? dimensions)
    {
        if (memory.Length <= ChunkLength)
        {
            return FromImmutableMemory(memory, dimensions);
        }

        var half = (memory.Length / ChunkLength + 1) / 2 * ChunkLength;
        return ConcatDirect(Chunked(memory[..half], dimensions), Chunked(memory[half..], dimensions));
    }

    /// <summary>
    /// Leaves shorter than this are merged into a neighbouring leaf when concatenated, so that edits made one
    /// character at a time do not leave a tree of single-character leaves.
    /// </summary>
    const int MergeLeafLength = 512;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static SumTree<T> Concat(SumTree<T> left, SumTree<T> right)
    {
        if (left.IsEmpty)
        {
            return right;
        }

        if (right.IsEmpty)
        {
            return left;
        }

        if (!right.IsNode && right._length < MergeLeafLength && AppendToLastLeaf(left, right) is { } appended)
        {
            return appended;
        }

        if (!left.IsNode && left._length < MergeLeafLength && PrependToFirstLeaf(right, left) is { } prepended)
        {
            return prepended;
        }

        return ConcatNodes(left, right);
    }

    /// <summary>Merges a small leaf into the tree's last leaf, or returns null when that leaf is too long.</summary>
    static SumTree<T>? AppendToLastLeaf(SumTree<T> tree, SumTree<T> leaf)
    {
        if (!tree.IsNode)
        {
            return tree._length + leaf._length <= MergeLeafLength ? MergeLeaves(tree, leaf) : null;
        }

        return AppendToLastLeaf(tree.Right, leaf) is { } right ? ConcatNodes(tree.Left, right) : null;
    }

    /// <summary>Merges a small leaf into the tree's first leaf, or returns null when that leaf is too long.</summary>
    static SumTree<T>? PrependToFirstLeaf(SumTree<T> tree, SumTree<T> leaf)
    {
        if (!tree.IsNode)
        {
            return tree._length + leaf._length <= MergeLeafLength ? MergeLeaves(leaf, tree) : null;
        }

        return PrependToFirstLeaf(tree.Left, leaf) is { } left ? ConcatNodes(left, tree.Right) : null;
    }

    /// <summary>Copies two leaves into one.</summary>
    static SumTree<T> MergeLeaves(SumTree<T> left, SumTree<T> right)
    {
        var buffer = new T[left._length + right._length];
        var index = 0;
        left.CopyTo(buffer, ref index);
        right.CopyTo(buffer, ref index);
        return FromImmutableMemory(buffer.AsMemory(), MergeDimensions(left._dimensions, right._dimensions));
    }

    /// <summary>Joins two non-empty trees under a new node, rebalancing when the result is too deep.</summary>
    static SumTree<T> ConcatNodes(SumTree<T> left, SumTree<T> right)
    {
        if (left.IsEmpty)
        {
            return right;
        }

        if (right.IsEmpty)
        {
            return left;
        }

        var newLength = left._length + right._length;
        var newDepth = Math.Max(left._depth, right._depth) + 1;
        var newBufferCount = left._bufferCount + right._bufferCount;
        var isBalanced = CalculateIsBalanced(newLength, newDepth);

        // Merge dimensions
        var dimensions = MergeDimensions(left._dimensions, right._dimensions);
        left = left.WithDimensions(dimensions);
        right = right.WithDimensions(dimensions);
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

    SumTree<T> Balanced()
    {
        if (_isBalanced)
        {
            return this;
        }

        t_balancedCallDepth++;
        try
        {
            if (t_balancedCallDepth > MaxTreeDepth)
            {
                throw new InvalidOperationException("Tree depth exceeded maximum");
            }

            var leaves = new List<SumTree<T>>();
            CollectLeaves(leaves);
            return CombineLeaves(leaves);
        }
        finally
        {
            t_balancedCallDepth--;
        }
    }

    void CollectLeaves(List<SumTree<T>> leaves)
    {
        if (!IsNode)
        {
            leaves.Add(this);
            return;
        }

        Left.CollectLeaves(leaves);
        Right.CollectLeaves(leaves);
    }

    static SumTree<T> CombineLeaves(List<SumTree<T>> leaves)
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

    static SumTree<T> ConcatDirect(SumTree<T> left, SumTree<T> right)
    {
        if (left.IsEmpty)
        {
            return right;
        }

        if (right.IsEmpty)
        {
            return left;
        }

        var newLength = left._length + right._length;
        var newDepth = Math.Max(left._depth, right._depth) + 1;
        var newBufferCount = left._bufferCount + right._bufferCount;
        var isBalanced = CalculateIsBalanced(newLength, newDepth);

        // Merge dimensions
        var dimensions = MergeDimensions(left._dimensions, right._dimensions);
        left = left.WithDimensions(dimensions);
        right = right.WithDimensions(dimensions);
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

    SumTree<T> WithDimensions(Dictionary<Type, object>? newDimensions)
    {
        if (newDimensions == null || newDimensions.Count == 0)
        {
            return this;
        }

        if (_dimensions != null && DimensionsEqual(_dimensions, newDimensions))
        {
            return this;
        }

        if (IsNode)
        {
            var left = Left.WithDimensions(newDimensions);
            var right = Right.WithDimensions(newDimensions);
            var summaries = ComputeCombinedSummaries(left, right, newDimensions);
            return new SumTree<T>((left, right), _length, _depth, _bufferCount, _isBalanced,
                newDimensions, summaries);
        }

        // Discard cached values when a dimension instance is replaced. Asking the old
        // tree for its summary would return the value computed by the previous instance.
        var uncached = new SumTree<T>(_data, _length, _depth, _bufferCount, _isBalanced, newDimensions);
        var newSummaries = uncached.ComputeAllSummaries(newDimensions);
        return new SumTree<T>(_data, _length, _depth, _bufferCount, _isBalanced, newDimensions, newSummaries);
    }

    /// <summary>Scans one leaf for the first element whose running summary satisfies the predicate.</summary>
    long FindPositionInLeaf<TSummary>(
        ISummaryDimension<T, TSummary> dimension,
        Func<TSummary, bool> predicate,
        TSummary summary,
        long offset) where TSummary : IEquatable<TSummary>
    {
        switch (_data)
        {
            case ReadOnlyMemory<T> memory:
                var span = memory.Span;
                for (var i = 0; i < span.Length; i++)
                {
                    summary = dimension.Combine(summary, dimension.SummarizeElement(span[i]));
                    if (predicate(summary))
                    {
                        return offset + i;
                    }
                }

                return -1;
            case ValueTuple<T> single:
                return predicate(dimension.Combine(summary, dimension.SummarizeElement(single.Item1))) ? offset : -1;
            default:
                return -1;
        }
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
        {
            return (TSummary)summary;
        }

        return ComputeSummaryForTree(dimension);
    }

    /// <summary>Summarizes a range without constructing a slice. Register the dimension to use cached subtrees.</summary>
    public TSummary GetRangeSummary<TSummary>(long start, long length, ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        ArgumentNullException.ThrowIfNull(dimension);
        if (start < 0 || start > Length) throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || length > Length - start) throw new ArgumentOutOfRangeException(nameof(length));
        if (length == 0) return dimension.Identity;
        if (start == 0 && length == Length) return GetSummary(dimension);
        if (!IsNode) return _data switch
        {
            ReadOnlyMemory<T> memory => dimension.SummarizeSpan(memory.Span.Slice((int)start, (int)length)),
            ValueTuple<T> single => dimension.SummarizeElement(single.Item1),
            _ => dimension.Identity
        };
        var left = Left;
        if (start >= left.Length) return Right.GetRangeSummary(start - left.Length, length, dimension);
        var count = Math.Min(length, left.Length - start);
        var prefix = left.GetRangeSummary(start, count, dimension);
        return count == length ? prefix : dimension.Combine(prefix, Right.GetRangeSummary(0, length - count, dimension));
    }

    /// <summary>Copies exactly destination.Length elements starting at start.</summary>
    public void CopyTo(long start, Span<T> destination)
    {
        if (start < 0 || start > Length) throw new ArgumentOutOfRangeException(nameof(start));
        if (destination.Length > Length - start) throw new ArgumentException("Range exceeds tree length.", nameof(destination));
        if (destination.IsEmpty) return;
        if (!IsNode)
        {
            if (_data is ReadOnlyMemory<T> memory) memory.Span.Slice((int)start, destination.Length).CopyTo(destination);
            else if (_data is ValueTuple<T> single) destination[0] = single.Item1;
            return;
        }
        var left = Left;
        if (start >= left.Length) { Right.CopyTo(start - left.Length, destination); return; }
        var count = (int)Math.Min(destination.Length, left.Length - start);
        left.CopyTo(start, destination[..count]);
        if (count < destination.Length) Right.CopyTo(0, destination[count..]);
    }

    TSummary ComputeSummaryForTree<TSummary>(ISummaryDimension<T, TSummary> dimension)
        where TSummary : IEquatable<TSummary>
    {
        if (IsEmpty)
        {
            return dimension.Identity;
        }

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

    static Dictionary<Type, object>? MergeDimensions(
        Dictionary<Type, object>? left,
        Dictionary<Type, object>? right)
    {
        if (left == null)
        {
            return right;
        }

        if (right == null)
        {
            return left;
        }

        if (ReferenceEquals(left, right) || DimensionsEqual(left, right))
        {
            return left;
        }

        var result = new Dictionary<Type, object>(left);
        foreach (var kvp in right)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    static Dictionary<Type, object>? ComputeCombinedSummaries(
        SumTree<T> left,
        SumTree<T> right,
        Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>(dimensions.Count);

        foreach (var kvp in dimensions)
        {
            var operations = DimensionOperations.For(kvp.Value);
            var leftSummary = left._summaries?.GetValueOrDefault(kvp.Key) ?? operations.SummarizeTree(left);
            var rightSummary = right._summaries?.GetValueOrDefault(kvp.Key) ?? operations.SummarizeTree(right);
            result[kvp.Key] = operations.Combine(leftSummary, rightSummary);
        }

        return result;
    }

    Dictionary<Type, object> ComputeAllSummaries(Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>(dimensions.Count);

        foreach (var kvp in dimensions)
        {
            result[kvp.Key] = DimensionOperations.For(kvp.Value).SummarizeTree(this);
        }

        return result;
    }

    static Dictionary<Type, object> ComputeEmptySummaries(Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>(dimensions.Count);

        foreach (var kvp in dimensions)
        {
            result[kvp.Key] = DimensionOperations.For(kvp.Value).Identity;
        }

        return result;
    }

    static Dictionary<Type, object> ComputeSummariesForMemory(ReadOnlyMemory<T> memory,
        Dictionary<Type, object> dimensions)
    {
        var result = new Dictionary<Type, object>(dimensions.Count);

        foreach (var kvp in dimensions)
        {
            result[kvp.Key] = DimensionOperations.For(kvp.Value).SummarizeMemory(memory);
        }

        return result;
    }

    /// <summary>
    /// Untyped delegates over one registered dimension, built by reflection once per dimension instance so that
    /// concatenation and leaf creation combine summaries without reflective calls.
    /// </summary>
    sealed class DimensionOperations
    {
        static readonly ConditionalWeakTable<object, DimensionOperations> s_cache = new();

        static readonly MethodInfo s_create = typeof(DimensionOperations).GetMethod(nameof(Create),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        DimensionOperations(Func<object, object, object> combine, Func<ReadOnlyMemory<T>, object> memory,
            Func<SumTree<T>, object> tree, object identity)
        {
            Combine = combine;
            SummarizeMemory = memory;
            SummarizeTree = tree;
            Identity = identity;
        }

        /// <summary>Combines two boxed summaries.</summary>
        internal Func<object, object, object> Combine { get; }

        /// <summary>Summarizes a leaf's elements.</summary>
        internal Func<ReadOnlyMemory<T>, object> SummarizeMemory { get; }

        /// <summary>Summarizes a whole tree, recursing where no summary is cached.</summary>
        internal Func<SumTree<T>, object> SummarizeTree { get; }

        /// <summary>The boxed empty summary.</summary>
        internal object Identity { get; }

        /// <summary>The operations of a dimension instance, cached for its lifetime.</summary>
        internal static DimensionOperations For(object dimension) => s_cache.GetValue(dimension, Build);

        static DimensionOperations Build(object dimension)
        {
            var summaryInterface = dimension.GetType().GetInterfaces().FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>)
                && i.GetGenericArguments()[0] == typeof(T))
                ?? throw new ArgumentException($"Invalid dimension type: {dimension.GetType()}");

            var create = s_create.MakeGenericMethod(summaryInterface.GetGenericArguments()[1]);
            return (DimensionOperations)create.Invoke(null, [dimension])!;
        }

        static DimensionOperations Create<TSummary>(ISummaryDimension<T, TSummary> dimension)
            where TSummary : IEquatable<TSummary> => new(
            (left, right) => dimension.Combine((TSummary)left, (TSummary)right),
            memory => dimension.SummarizeSpan(memory.Span),
            tree => tree.GetSummary(dimension),
            dimension.Identity);
    }

    static bool DimensionsEqual(Dictionary<Type, object>? left, Dictionary<Type, object>? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var kvp in left)
        {
            if (!right.TryGetValue(kvp.Key, out var rightValue) || !kvp.Value.Equals(rightValue))
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool CalculateIsBalanced(long length, int depth)
    {
        return depth < MaxTreeDepth && (depth == 0 || length >= s_fibonacciPlusTwo[depth]);
    }

    static int Fibonacci(int n)
    {
        if (n <= 1)
        {
            return n;
        }

        int a = 0, b = 1;
        for (var i = 2; i <= n; i++)
        {
            var temp = a + b;
            a = b;
            b = temp;
        }
        return b;
    }

    static int CalculateMaxLeafLength()
    {
        var elementSize = Unsafe.SizeOf<T>();
        var maxElements = LargeObjectHeapBytes / elementSize;
        const int cacheLineSize = 64;
        var alignedElements = (maxElements / cacheLineSize) * cacheLineSize;
        return Math.Max(alignedElements, cacheLineSize);
    }
}
