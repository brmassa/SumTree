using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SumTree;

/// <summary>A persistent rope with a value-type summary and statically dispatched operations.</summary>
/// <remarks>
/// This typed path avoids summary dictionaries, boxing, and runtime operation lookup.
/// It retains binary rope nodes and Fibonacci balancing; the B+ tree migration is separate.
/// Mutable buffers are copied, strings are shared, and internal slices retain owned storage.
/// Elements and summaries must be immutable. Use synchronization or a reference holder when
/// publishing this multi-field struct. Index/range arguments are validated strictly.
/// </remarks>
public readonly struct SumTree<T, TSummary, TOps> : IEnumerable<T>, IReadOnlyList<T>
    where T : IEquatable<T>
    where TSummary : struct
    where TOps : struct, ISummaryOperations<T, TSummary>
{
    const int ChunkLength = 1024;
    const int MergeLeafLength = 512;
    const int MaxTreeDepth = 46;
    static readonly long[] s_minLengths = MinimumLengths();
    readonly object? _data;
    readonly TSummary _summary;
    readonly long _length;
    readonly int _depth;
    readonly int _bufferCount;

    public static SumTree<T, TSummary, TOps> Empty => default;
    public long Length => _length;
    public int Count => checked((int)_length);
    public int Depth => _depth;
    public int BufferCount => _bufferCount;
    public bool IsEmpty => _length == 0;
    public TSummary Summary => IsEmpty ? TOps.Identity : _summary;
    bool IsNode => _data is ValueTuple<SumTree<T, TSummary, TOps>, SumTree<T, TSummary, TOps>>;
    bool IsBalanced => _depth < MaxTreeDepth && _length >= s_minLengths[_depth];
    (SumTree<T, TSummary, TOps> Left, SumTree<T, TSummary, TOps> Right) Children =>
        ((SumTree<T, TSummary, TOps>, SumTree<T, TSummary, TOps>))_data!;

    public SumTree(ReadOnlyMemory<T> memory)
    {
        this = FromImmutableMemory(SnapshotMemory.Copy(memory));
    }

    SumTree(ReadOnlyMemory<T> leaf, TSummary summary)
    {
        _data = leaf;
        _summary = summary;
        _length = leaf.Length;
        _depth = 0;
        _bufferCount = 1;
    }

    SumTree(SumTree<T, TSummary, TOps> left, SumTree<T, TSummary, TOps> right)
    {
        _data = (left, right);
        _summary = TOps.Combine(in left._summary, in right._summary);
        _length = checked(left._length + right._length);
        _depth = Math.Max(left._depth, right._depth) + 1;
        _bufferCount = checked(left._bufferCount + right._bufferCount);
    }

    /// <summary>Shares caller storage without copying.</summary>
    /// <remarks>Keep it valid and unchanged while this or any derived snapshot exists, including pooled buffers.</remarks>
    public static SumTree<T, TSummary, TOps> FromBorrowedMemory(ReadOnlyMemory<T> memory) => FromImmutableMemory(memory);

    internal static SumTree<T, TSummary, TOps> FromImmutableMemory(ReadOnlyMemory<T> memory)
    {
        if (memory.IsEmpty)
        {
            return Empty;
        }

        if (memory.Length <= ChunkLength)
        {
            return new(memory, TOps.SummarizeSpan(memory.Span));
        }

        var half = (memory.Length / ChunkLength + 1) / 2 * ChunkLength;
        return new(FromImmutableMemory(memory[..half]), FromImmutableMemory(memory[half..]));
    }

    public T this[int index] => this[(long)index];
    public T this[long index]
    {
        get
        {
            ValidateIndex(index, allowEnd: false);
            var current = this;
            while (current.IsNode)
            {
                var (left, right) = current.Children;
                if (index < left._length)
                {
                    current = left;
                }
                else
                {
                    index -= left._length;
                    current = right;
                }
            }

            return ((ReadOnlyMemory<T>)current._data!).Span[(int)index];
        }
    }

    public SumTree<T, TSummary, TOps> Add(T item) => AddRange(FromImmutableMemory(new[] { item }));
    public SumTree<T, TSummary, TOps> AddRange(ReadOnlyMemory<T> items) =>
        AddRange(new SumTree<T, TSummary, TOps>(items));
    public SumTree<T, TSummary, TOps> AddRange(SumTree<T, TSummary, TOps> other) => Concat(this, other);
    public SumTree<T, TSummary, TOps> Insert(long index, T item) =>
        InsertRange(index, FromImmutableMemory(new[] { item }));

    public SumTree<T, TSummary, TOps> InsertRange(long index, ReadOnlyMemory<T> items)
    {
        ValidateIndex(index, allowEnd: true);
        return InsertRange(index, new SumTree<T, TSummary, TOps>(items));
    }

    public SumTree<T, TSummary, TOps> InsertRange(long index, SumTree<T, TSummary, TOps> items)
    {
        ValidateIndex(index, allowEnd: true);
        if (items.IsEmpty)
        {
            return this;
        }

        if (index == 0)
        {
            return Concat(items, this);
        }

        if (index == _length)
        {
            return Concat(this, items);
        }

        var (left, right) = SplitAt(index);
        return Concat(Concat(left, items), right);
    }

    public (SumTree<T, TSummary, TOps> Left, SumTree<T, TSummary, TOps> Right) SplitAt(long index)
    {
        ValidateIndex(index, allowEnd: true);
        if (index == 0)
        {
            return (Empty, this);
        }

        if (index == _length)
        {
            return (this, Empty);
        }

        if (!IsNode)
        {
            var memory = (ReadOnlyMemory<T>)_data!;
            return (FromImmutableMemory(memory[..(int)index]), FromImmutableMemory(memory[(int)index..]));
        }

        var (left, right) = Children;
        if (index == left._length)
        {
            return (left, right);
        }

        if (index < left._length)
        {
            var (before, after) = left.SplitAt(index);
            return (before, Concat(after, right));
        }

        var (prefix, suffix) = right.SplitAt(index - left._length);
        return (Concat(left, prefix), suffix);
    }

    public SumTree<T, TSummary, TOps> RemoveAt(long index) => RemoveRange(index, 1);
    public SumTree<T, TSummary, TOps> RemoveRange(long start, long length)
    {
        ValidateRange(start, length);
        if (length == 0)
        {
            return this;
        }

        var (left, remaining) = SplitAt(start);
        var (_, right) = remaining.SplitAt(length);
        return Concat(left, right);
    }

    public SumTree<T, TSummary, TOps> Slice(long start, long length)
    {
        ValidateRange(start, length);
        if (length == 0)
        {
            return Empty;
        }

        var (_, remaining) = SplitAt(start);
        return remaining.SplitAt(length).Left;
    }

    /// <summary>Finds the first element whose inclusive prefix reaches a monotonic target.</summary>
    public long FindPosition<TSeek>(TSeek seek) where TSeek : struct, ISummarySeek<TSummary>
    {
        if (IsEmpty || !seek.IsReached(in _summary))
        {
            return -1;
        }

        var current = this;
        var prefix = TOps.Identity;
        var offset = 0L;
        while (current.IsNode)
        {
            var (left, right) = current.Children;
            var combined = TOps.Combine(in prefix, in left._summary);
            if (seek.IsReached(in combined))
            {
                current = left;
            }
            else
            {
                prefix = combined;
                offset += left._length;
                current = right;
            }
        }

        var span = ((ReadOnlyMemory<T>)current._data!).Span;
        for (var i = 0; i < span.Length; i++)
        {
            var element = TOps.SummarizeElement(span[i]);
            prefix = TOps.Combine(in prefix, in element);
            if (seek.IsReached(in prefix))
            {
                return offset + i;
            }
        }

        return -1;
    }

    public long FindPosition(Func<TSummary, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return FindPosition(new PredicateSeek(predicate));
    }

    readonly struct PredicateSeek(Func<TSummary, bool> predicate) : ISummarySeek<TSummary>
    {
        public bool IsReached(in TSummary summary) => predicate(summary);
    }

    public T[] ToArray()
    {
        var result = new T[checked((int)_length)];
        CopyTo(result);
        return result;
    }

    public void CopyTo(Span<T> destination)
    {
        if (destination.Length < _length)
        {
            throw new ArgumentException("Destination is too short.", nameof(destination));
        }

        if (IsEmpty)
        {
            return;
        }

        if (!IsNode)
        {
            ((ReadOnlyMemory<T>)_data!).Span.CopyTo(destination);
            return;
        }
        var (left, right) = Children;
        left.CopyTo(destination);
        right.CopyTo(destination[checked((int)left._length)..]);
    }

    public override string ToString()
    {
        if (typeof(T) != typeof(char))
        {
            return $"SumTree<{typeof(T).Name}>[Length={_length}, Depth={_depth}]";
        }

        return string.Create(checked((int)_length), this, static (destination, tree) =>
        {
            var elements = MemoryMarshal.CreateSpan(ref Unsafe.As<char, T>(ref MemoryMarshal.GetReference(destination)),
                destination.Length);
            tree.CopyTo(elements);
        });
    }

    public IEnumerator<T> GetEnumerator()
    {
        if (IsEmpty)
        {
            yield break;
        }

        if (!IsNode)
        {
            var memory = (ReadOnlyMemory<T>)_data!;
            for (var i = 0; i < memory.Length; i++)
            {
                yield return memory.Span[i];
            }

            yield break;
        }

        var (left, right) = Children;
        foreach (var item in left)
        {
            yield return item;
        }

        foreach (var item in right)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public static SumTree<T, TSummary, TOps> operator +(SumTree<T, TSummary, TOps> left,
        SumTree<T, TSummary, TOps> right) => Concat(left, right);

    static SumTree<T, TSummary, TOps> Concat(SumTree<T, TSummary, TOps> left, SumTree<T, TSummary, TOps> right)
    {
        if (left.IsEmpty)
        {
            return right;
        }

        if (right.IsEmpty)
        {
            return left;
        }

        if (!right.IsNode && right._length < MergeLeafLength && AppendLeaf(left, right) is { } appended)
        {
            return appended;
        }

        if (!left.IsNode && left._length < MergeLeafLength && PrependLeaf(right, left) is { } prepended)
        {
            return prepended;
        }

        return Join(left, right);
    }

    static SumTree<T, TSummary, TOps>? AppendLeaf(SumTree<T, TSummary, TOps> tree, SumTree<T, TSummary, TOps> leaf)
    {
        if (!tree.IsNode)
        {
            return tree._length + leaf._length <= MergeLeafLength ? Merge(tree, leaf) : null;
        }

        var (left, right) = tree.Children;
        return AppendLeaf(right, leaf) is { } appended ? Join(left, appended) : null;
    }

    static SumTree<T, TSummary, TOps>? PrependLeaf(SumTree<T, TSummary, TOps> tree, SumTree<T, TSummary, TOps> leaf)
    {
        if (!tree.IsNode)
        {
            return tree._length + leaf._length <= MergeLeafLength ? Merge(leaf, tree) : null;
        }

        var (left, right) = tree.Children;
        return PrependLeaf(left, leaf) is { } prepended ? Join(prepended, right) : null;
    }

    static SumTree<T, TSummary, TOps> Merge(SumTree<T, TSummary, TOps> left, SumTree<T, TSummary, TOps> right)
    {
        var buffer = new T[checked((int)(left._length + right._length))];
        left.CopyTo(buffer);
        right.CopyTo(buffer.AsSpan((int)left._length));
        return FromImmutableMemory(buffer);
    }

    static SumTree<T, TSummary, TOps> Join(SumTree<T, TSummary, TOps> left, SumTree<T, TSummary, TOps> right)
    {
        if (left.IsEmpty)
        {
            return right;
        }

        if (right.IsEmpty)
        {
            return left;
        }

        var result = new SumTree<T, TSummary, TOps>(left, right);
        if (result.IsBalanced)
        {
            return result;
        }

        var leaves = new List<SumTree<T, TSummary, TOps>>();
        result.CollectLeaves(leaves);
        return Rebuild(leaves, 0, leaves.Count);
    }

    void CollectLeaves(List<SumTree<T, TSummary, TOps>> leaves)
    {
        if (!IsNode)
        {
            leaves.Add(this);
            return;
        }
        var (left, right) = Children;
        left.CollectLeaves(leaves);
        right.CollectLeaves(leaves);
    }

    static SumTree<T, TSummary, TOps> Rebuild(List<SumTree<T, TSummary, TOps>> leaves, int start, int count)
    {
        if (count == 1)
        {
            return leaves[start];
        }

        var half = count / 2;
        return new(Rebuild(leaves, start, half), Rebuild(leaves, start + half, count - half));
    }

    void ValidateIndex(long index, bool allowEnd)
    {
        if (index < 0 || index > _length || (!allowEnd && index == _length))
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    void ValidateRange(long start, long length)
    {
        ValidateIndex(start, allowEnd: true);
        if (length < 0 || length > _length - start)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }

    static long[] MinimumLengths()
    {
        var result = new long[MaxTreeDepth];
        var previous = 0L;
        var current = 1L;
        for (var i = 1; i < result.Length; i++)
        {
            result[i] = current + 2;
            (previous, current) = (current, previous + current);
        }

        return result;
    }
}
