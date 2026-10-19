namespace SumTree;

/// <summary>
/// Accumulates elements and builds a <see cref="SumTree{T, TSummary, TOps}"/> in bulk.
/// Appending single elements to an immutable tree copies the boundary leaf on every call;
/// this builder batches them into full chunks so bulk construction stays linear.
/// </summary>
/// <typeparam name="T">The type of elements in the tree.</typeparam>
/// <typeparam name="TSummary">The summary type of the tree.</typeparam>
/// <typeparam name="TOps">The summary operations.</typeparam>
public sealed class SumTreeBuilder<T, TSummary, TOps>
    where T : IEquatable<T>
    where TSummary : struct
    where TOps : struct, ISummaryOperations<T, TSummary>
{
    readonly T[] _buffer = new T[SumTree<T, TSummary, TOps>.ChunkLength];
    SumTree<T, TSummary, TOps> _tree;
    int _count;

    /// <summary>Creates an empty builder.</summary>
    public SumTreeBuilder()
    {
    }

    /// <summary>Creates a builder that appends to an existing tree.</summary>
    public SumTreeBuilder(SumTree<T, TSummary, TOps> tree)
    {
        _tree = tree;
    }

    /// <summary>Gets the number of elements added so far, including buffered ones.</summary>
    public long Length => _tree.Length + _count;

    /// <summary>Gets whether nothing has been added.</summary>
    public bool IsEmpty => Length == 0;

    /// <summary>Buffers one element.</summary>
    public void Add(T item)
    {
        if (_count == _buffer.Length)
        {
            FlushBuffer();
        }

        _buffer[_count++] = item;
    }

    /// <summary>Buffers a span of elements in order.</summary>
    public void AddRange(ReadOnlySpan<T> items)
    {
        while (!items.IsEmpty)
        {
            var space = _buffer.Length - _count;
            var take = Math.Min(space, items.Length);
            items[..take].CopyTo(_buffer.AsSpan(_count));
            _count += take;
            items = items[take..];

            if (_count == _buffer.Length)
            {
                FlushBuffer();
            }
        }
    }

    /// <summary>Appends a memory block of elements in order.</summary>
    /// <remarks>Large blocks are appended directly; small ones are buffered until a full chunk.</remarks>
    public void AddRange(ReadOnlyMemory<T> items)
    {
        if (_count == 0 && items.Length >= SumTree<T, TSummary, TOps>.ChunkLength)
        {
            _tree = _tree.AddRange(items);
            return;
        }

        AddRange(items.Span);
    }

    /// <summary>Appends every element of a sequence, in order.</summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        switch (items)
        {
            case T[] array:
                AddRange(array.AsSpan());
                return;
            case List<T> list:
                AddRange(CollectionsMarshal.AsSpan(list));
                return;
            case SumTree<T, TSummary, TOps> tree:
                AddRange(tree);
                return;
            case IReadOnlyList<T> readOnlyList:
                for (var i = 0; i < readOnlyList.Count; i++)
                {
                    Add(readOnlyList[i]);
                }

                return;
            default:
                foreach (var item in items)
                {
                    Add(item);
                }

                return;
        }
    }

    /// <summary>Appends another tree after the buffered elements.</summary>
    public void AddRange(SumTree<T, TSummary, TOps> tree)
    {
        if (tree.IsEmpty)
        {
            return;
        }

        FlushBuffer();
        _tree = _tree.AddRange(tree);
    }

    /// <summary>Flushes the buffer and returns the built tree.</summary>
    public SumTree<T, TSummary, TOps> ToTree()
    {
        FlushBuffer();
        return _tree;
    }

    void FlushBuffer()
    {
        if (_count == 0)
        {
            return;
        }

        _tree = _tree.AddRange(_buffer.AsMemory(0, _count));
        _count = 0;
    }
}
