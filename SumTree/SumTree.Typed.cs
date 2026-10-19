using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SumTree;

/// <summary>A persistent B+ rope with inline typed summaries and local split/join balancing.</summary>
/// <remarks>
/// Mutable buffers are copied, strings are shared, and internal slices retain owned storage.
/// Nodes are immutable after construction; edits copy affected paths and share other subtrees.
/// Elements and summaries must be immutable. Publish versions through a snapshot store or
/// external synchronization. Index/range arguments are validated strictly.
/// </remarks>
public readonly struct SumTree<T, TSummary, TOps> : IReadOnlyList<T>, IEquatable<SumTree<T, TSummary, TOps>>
    where T : IEquatable<T>
    where TSummary : struct
    where TOps : struct, ISummaryOperations<T, TSummary>
{
    internal const int ChunkLength = 1024;
    internal const int MaximumChildren = 8;
    const int MinimumChildren = MaximumChildren / 2;
    const int MergeLeafLength = 512;
    readonly Node? _root;

    public static SumTree<T, TSummary, TOps> Empty => default;
    public long Length => _root?.Length ?? 0;
    public int Count => (int)Math.Min(Length, int.MaxValue);
    public int Depth => _root?.Depth ?? 0;
    public int BufferCount => _root?.BufferCount ?? 0;
    public bool IsEmpty => _root is null;
    public TSummary Summary => _root is { } root ? root.Summary : TOps.Identity;

    public SumTree(ReadOnlyMemory<T> memory)
    {
        _root = Build(SnapshotMemory.Copy(memory));
    }

    SumTree(Node? root) => _root = root;

    /// <summary>Shares caller storage without copying.</summary>
    /// <remarks>Keep it valid and unchanged while this or any derived snapshot exists, including pooled buffers.</remarks>
    public static SumTree<T, TSummary, TOps> FromBorrowedMemory(ReadOnlyMemory<T> memory) => FromImmutableMemory(memory);

    internal static SumTree<T, TSummary, TOps> FromImmutableMemory(ReadOnlyMemory<T> memory) => new(Build(memory));

    public T this[int index] => this[(long)index];
    public T this[long index]
    {
        get
        {
            ValidateIndex(index, allowEnd: false);
            var current = _root!;
            while (current is Branch branch)
            {
                var i = 0;
                while (index >= branch[i].Length)
                {
                    index -= branch[i++].Length;
                }

                current = branch[i];
            }

            return ((Leaf)current).Memory.Span[(int)index];
        }
    }

    public SumTree<T, TSummary, TOps> Add(T item) => AddRange(FromImmutableMemory(new[] { item }));
    public SumTree<T, TSummary, TOps> AddRange(ReadOnlyMemory<T> items) =>
        AddRange(new SumTree<T, TSummary, TOps>(items));
    public SumTree<T, TSummary, TOps> AddRange(SumTree<T, TSummary, TOps> other) => new(JoinRoots(_root, other._root));
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

        if (IsEmpty)
        {
            return items;
        }
        // A short insertion replaces one leaf and copies one path, splitting full nodes locally.
        if (items._root is Leaf leaf)
        {
            return new(RootOf(InsertLeaf(_root!, index, leaf)));
        }

        var (left, right) = Split(_root, index);
        return new(JoinRoots(JoinRoots(left, items._root), right));
    }

    public (SumTree<T, TSummary, TOps> Left, SumTree<T, TSummary, TOps> Right) SplitAt(long index)
    {
        ValidateIndex(index, allowEnd: true);
        var (left, right) = Split(_root, index);
        return (new(left), new(right));
    }

    public SumTree<T, TSummary, TOps> RemoveAt(long index) => RemoveRange(index, 1);
    public SumTree<T, TSummary, TOps> RemoveRange(long start, long length)
    {
        ValidateRange(start, length);
        if (length == 0)
        {
            return this;
        }

        if (length == Length)
        {
            return Empty;
        }

        if (length <= ChunkLength)
        {
            var updated = RemoveFromLeaf(_root!, start, length, out var handled);
            if (handled)
            {
                return new(NormalizeRoot(updated));
            }
        }
        var (left, remaining) = Split(_root, start);
        var (_, right) = Split(remaining, length);
        return new(JoinRoots(left, right));
    }

    public SumTree<T, TSummary, TOps> Slice(long start, long length)
    {
        ValidateRange(start, length);
        if (length == 0)
        {
            return Empty;
        }

        var (_, remaining) = Split(_root, start);
        return new(Split(remaining, length).Left);
    }

    /// <summary>Summarizes [0, length) without creating a tree or allocating.</summary>
    public TSummary GetPrefixSummary(long length) => GetRangeSummary(0, length);

    /// <summary>Summarizes a range using cached complete subtrees and scanning only boundary leaves.</summary>
    /// <remarks>Does not require subtraction or an invertible summary operation.</remarks>
    public TSummary GetRangeSummary(long start, long length)
    {
        ValidateRange(start, length);
        return length == 0 ? TOps.Identity : SummarizeRange(_root!, start, length);
    }

    static TSummary SummarizeRange(Node node, long start, long length)
    {
        if (start == 0 && length == node.Length)
        {
            return node.Summary;
        }

        if (node is Leaf leaf)
        {
            return TOps.SummarizeSpan(leaf.Memory.Span.Slice((int)start, (int)length));
        }

        var summary = TOps.Identity;
        var branch = (Branch)node;
        for (var i = 0; i < branch.ChildCount && length > 0; i++)
        {
            var child = branch[i];
            if (start >= child.Length) { start -= child.Length; continue; }
            var count = Math.Min(length, child.Length - start);
            var part = SummarizeRange(child, start, count);
            summary = TOps.Combine(in summary, in part);
            length -= count;
            start = 0;
        }
        return summary;
    }

    /// <summary>Visits leaf spans in order, with no allocations or per-element tree descent.</summary>
    public ChunkEnumerator GetChunks() => new(this, 0, Length);
    public ChunkEnumerator GetChunks(long start, long length)
    {
        ValidateRange(start, length);
        return new(this, start, length);
    }

    /// <summary>Copies exactly destination.Length elements starting at start.</summary>
    public void CopyTo(long start, Span<T> destination)
    {
        var chunks = GetChunks(start, destination.Length);
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(destination);
            destination = destination[chunk.Length..];
        }
    }

    // BufferCount is an int and non-root branches have at least four children:
    // valid trees cannot approach 32 levels. Each enumerator owns its inline path.
    [InlineArray(32)]
    struct PathSlots { Frame _element0; }
    struct Frame(Branch branch, int index)
    {
        internal Branch Branch = branch;
        internal int Index = index;
    }

    public ref struct ChunkEnumerator
    {
        PathSlots _path;
        int _depth;
        Node? _next;
        long _start, _remaining;
        public ReadOnlySpan<T> Current { get; private set; }
        internal ChunkEnumerator(SumTree<T, TSummary, TOps> tree, long start, long length)
        {
            _path = default;
            _depth = 0;
            _next = tree._root;
            _start = start;
            _remaining = length;
            Current = default;
        }
        public readonly ChunkEnumerator GetEnumerator() => this;
        public bool MoveNext()
        {
            if (_remaining == 0) { Current = default; return false; }
            var node = _next;
            if (node is null)
            {
                while (_depth > 0)
                {
                    ref var frame = ref _path[_depth - 1];
                    if (++frame.Index < frame.Branch.ChildCount) { node = frame.Branch[frame.Index]; break; }
                    _depth--;
                }
            }
            while (node is Branch branch)
            {
                var index = 0;
                while (_start >= branch[index].Length)
                {
                    _start -= branch[index++].Length;
                }

                _path[_depth++] = new Frame(branch, index);
                node = branch[index];
            }
            var span = ((Leaf)node!).Memory.Span;
            var count = (int)Math.Min(_remaining, span.Length - _start);
            Current = span.Slice((int)_start, count);
            _remaining -= count;
            _start = 0;
            _next = null;
            return true;
        }
    }

    public ref struct Enumerator
    {
        ChunkEnumerator _chunks;
        ReadOnlySpan<T> _span;
        int _index;
        internal Enumerator(SumTree<T, TSummary, TOps> tree)
        { _chunks = tree.GetChunks(); _span = default; _index = -1; }
        public readonly T Current => _span[_index];
        public bool MoveNext()
        {
            if (++_index < _span.Length)
            {
                return true;
            }

            if (!_chunks.MoveNext())
            {
                return false;
            }

            _span = _chunks.Current;
            _index = 0;
            return true;
        }
        public readonly void Dispose() { }
    }

    /// <summary>Finds the first element whose inclusive prefix reaches a monotonic target.</summary>
    public long FindPosition<TSeek>(TSeek seek) where TSeek : struct, ISummarySeek<TSummary>
    {
        if (_root is not { } current || !seek.IsReached(in current.Summary))
        {
            return -1;
        }

        var prefix = TOps.Identity;
        var offset = 0L;
        while (current is Branch branch)
        {
            Node? next = null;
            for (var i = 0; i < branch.ChildCount; i++)
            {
                var child = branch[i];
                var combined = TOps.Combine(in prefix, in child.Summary);
                if (seek.IsReached(in combined)) { next = child; break; }
                prefix = combined;
                offset += child.Length;
            }

            if (next is null)
            {
                return -1;
            }

            current = next;
        }

        var span = ((Leaf)current).Memory.Span;
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
        var result = new T[checked((int)Length)];
        CopyTo(result);
        return result;
    }

    public void CopyTo(Span<T> destination)
    {
        if (destination.Length < Length)
        {
            throw new ArgumentException("Destination is too short.", nameof(destination));
        }

        if (_root is { } root)
        {
            Copy(root, destination);
        }
    }

    public override string ToString()
    {
        if (typeof(T) != typeof(char))
        {
            return $"SumTree<{typeof(T).Name}>[Length={Length}, Depth={Depth}]";
        }

        return string.Create(checked((int)Length), this, static (destination, tree) =>
        {
            var elements = MemoryMarshal.CreateSpan(ref Unsafe.As<char, T>(ref MemoryMarshal.GetReference(destination)),
                destination.Length);
            tree.CopyTo(elements);
        });
    }

    /// <summary>Enumerates with a stack-only span cursor and no allocations.</summary>
    /// <remarks>Use IEnumerable&lt;T&gt; when an interface enumerator is required; that adapter allocates.</remarks>
    public Enumerator GetEnumerator() => new(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => Enumerate(_root).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Enumerate(_root).GetEnumerator();
    public static SumTree<T, TSummary, TOps> operator +(SumTree<T, TSummary, TOps> left,
        SumTree<T, TSummary, TOps> right) => new(JoinRoots(left._root, right._root));

    /// <summary>Determines whether two trees contain the same elements in the same order.</summary>
    /// <remarks>
    /// Structural sharing is not considered; two separately built trees compare equal when their
    /// elements match, even if their leaf boundaries differ.
    /// </remarks>
    public bool Equals(SumTree<T, TSummary, TOps> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        var left = GetChunks();
        var right = other.GetChunks();
        var hasLeft = left.MoveNext();
        var hasRight = right.MoveNext();
        var a = hasLeft ? left.Current : default;
        var b = hasRight ? right.Current : default;

        while (true)
        {
            if (a.IsEmpty && hasLeft)
            {
                hasLeft = left.MoveNext();
                a = hasLeft ? left.Current : default;
                continue;
            }

            if (b.IsEmpty && hasRight)
            {
                hasRight = right.MoveNext();
                b = hasRight ? right.Current : default;
                continue;
            }

            if (a.IsEmpty && b.IsEmpty)
            {
                return !hasLeft && !hasRight;
            }

            if (a.IsEmpty || b.IsEmpty)
            {
                return false;
            }

            // Chunk boundaries may differ between the trees, so compare only the overlap
            // and keep the unconsumed remainder for the next round.
            var count = Math.Min(a.Length, b.Length);
            if (!a[..count].SequenceEqual(b[..count]))
            {
                return false;
            }

            a = a[count..];
            b = b[count..];
        }
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is SumTree<T, TSummary, TOps> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Length);
        foreach (var item in this)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(SumTree<T, TSummary, TOps> left, SumTree<T, TSummary, TOps> right) =>
        left.Equals(right);

    public static bool operator !=(SumTree<T, TSummary, TOps> left, SumTree<T, TSummary, TOps> right) =>
        !left.Equals(right);

    // Interface consumers cannot retain spans. Use one iterator and one traversal
    // stack rather than allocating recursive iterators for every node.
    static IEnumerable<T> Enumerate(Node? node)
    {
        if (node is null)
        {
            yield break;
        }

        var pending = new Stack<Node>();
        pending.Push(node);
        while (pending.TryPop(out node))
        {
            if (node is Leaf leaf)
            {
                for (var i = 0; i < leaf.Memory.Length; i++)
                {
                    yield return leaf.Memory.Span[i];
                }
            }
            else
            {
                var branch = (Branch)node;
                for (var i = branch.ChildCount - 1; i >= 0; i--)
                {
                    pending.Push(branch[i]);
                }
            }
        }
    }

    static void Copy(Node node, Span<T> destination)
    {
        if (node is Leaf leaf) { leaf.Memory.Span.CopyTo(destination); return; }
        var branch = (Branch)node;
        var offset = 0;
        for (var i = 0; i < branch.ChildCount; i++)
        {
            var child = branch[i];
            Copy(child, destination[offset..]);
            offset += checked((int)child.Length);
        }
    }

    static Node? Build(ReadOnlyMemory<T> memory)
    {
        if (memory.IsEmpty)
        {
            return null;
        }

        if (memory.Length <= ChunkLength)
        {
            return new Leaf(memory);
        }

        var leafCount = (memory.Length - 1) / ChunkLength + 1;
        var level = new Node[leafCount];
        for (var i = 0; i < leafCount; i++)
        {
            var start = i * ChunkLength;
            level[i] = new Leaf(memory.Slice(start, Math.Min(ChunkLength, memory.Length - start)));
        }

        while (level.Length > 1)
        {
            // Distribute the final group too: no underfull non-root branch at the end.
            var groups = (level.Length - 1) / MaximumChildren + 1;
            var parents = new Node[groups];
            var width = level.Length / groups;
            var extra = level.Length % groups;
            var start = 0;
            for (var group = 0; group < groups; group++)
            {
                var count = width + (group < extra ? 1 : 0);
                parents[group] = new Branch(level.AsSpan(start, count));
                start += count;
            }

            level = parents;
        }

        return level[0];
    }

    static Changes InsertLeaf(Node node, long index, Leaf inserted)
    {
        if (node is Leaf leaf)
        {
            if (index == 0)
            {
                return JoinLeaves(inserted, leaf);
            }

            if (index == leaf.Length)
            {
                return JoinLeaves(leaf, inserted);
            }

            var position = (int)index;
            if (leaf.Length + inserted.Length <= MergeLeafLength)
            {
                var buffer = new T[(int)(leaf.Length + inserted.Length)];
                leaf.Memory.Span[..position].CopyTo(buffer);
                inserted.Memory.Span.CopyTo(buffer.AsSpan(position));
                leaf.Memory.Span[position..].CopyTo(buffer.AsSpan(position + inserted.Memory.Length));
                return new(new Leaf(buffer));
            }

            var before = leaf.Memory[..position];
            var after = leaf.Memory[position..];
            if (before.Length + inserted.Length <= MergeLeafLength)
            {
                return new(Merge(before, inserted.Memory), new Leaf(after));
            }

            if (inserted.Length + after.Length <= MergeLeafLength)
            {
                return new(new Leaf(before), Merge(inserted.Memory, after));
            }

            return new(new Leaf(before), inserted, new Leaf(after));
        }

        var branch = (Branch)node;
        var childIndex = 0;
        while (index > branch[childIndex].Length)
        {
            index -= branch[childIndex++].Length;
        }

        var replacement = InsertLeaf(branch[childIndex], index, inserted);
        JoinSlots slots = default;
        Span<Node> children = slots;
        branch.Children[..childIndex].CopyTo(children);
        replacement.CopyTo(children[childIndex..]);
        branch.Children[(childIndex + 1)..].CopyTo(children[(childIndex + replacement.Count)..]);
        return Pack(children[..(branch.ChildCount - 1 + replacement.Count)]);
    }

    // A deletion confined to one leaf copies one path. Temporary underfull branches
    // keep their height until their parent merges or redistributes an adjacent pair.
    static Node? RemoveFromLeaf(Node node, long start, long length, out bool handled)
    {
        handled = true;
        if (node is Leaf leaf)
        {
            if (length == leaf.Length)
            {
                return null;
            }

            if (start == 0)
            {
                return new Leaf(leaf.Memory[(int)length..]);
            }

            if (start + length == leaf.Length)
            {
                return new Leaf(leaf.Memory[..(int)start]);
            }

            var buffer = new T[(int)(leaf.Length - length)];
            leaf.Memory.Span[..(int)start].CopyTo(buffer);
            leaf.Memory.Span[(int)(start + length)..].CopyTo(buffer.AsSpan((int)start));
            return new Leaf(buffer);
        }

        var branch = (Branch)node;
        var index = 0;
        while (start >= branch[index].Length)
        {
            start -= branch[index++].Length;
        }

        var child = branch[index];
        if (length > child.Length - start)
        {
            handled = false;
            return node;
        }

        var replacement = RemoveFromLeaf(child, start, length, out handled);
        if (!handled)
        {
            return node;
        }

        JoinSlots slots = default;
        Span<Node> children = slots;
        var count = branch.ChildCount;
        branch.Children[..index].CopyTo(children);
        if (replacement is null)
        {
            branch.Children[(index + 1)..].CopyTo(children[index..]);
            count--;
            index = Math.Min(index, count - 1);
        }
        else
        {
            children[index] = replacement;
            branch.Children[(index + 1)..].CopyTo(children[(index + 1)..]);
        }

        count = RepairChild(children, count, index);
        return count == 0 ? null : new Branch(children[..count]);
    }

    static int RepairChild(Span<Node> children, int count, int index)
    {
        if (count < 2)
        {
            return count;
        }

        var child = children[index];
        var pairIndex = -1;
        if (child is Branch branch && branch.ChildCount < MinimumChildren)
        {
            pairIndex = index > 0 ? index - 1 : 0;
        }
        else if (child is Leaf)
        {
            if (index > 0 && children[index - 1].Length + child.Length <= MergeLeafLength)
            {
                pairIndex = index - 1;
            }
            else if (index + 1 < count && child.Length + children[index + 1].Length <= MergeLeafLength)
            {
                pairIndex = index;
            }
        }

        if (pairIndex < 0)
        {
            return count;
        }

        var merged = Join(children[pairIndex], children[pairIndex + 1]);
        children[(pairIndex + 2)..count].CopyTo(children[(pairIndex + merged.Count)..]);
        merged.CopyTo(children[pairIndex..]);
        return count - 2 + merged.Count;
    }

    static Node? NormalizeRoot(Node? root)
    {
        while (root is Branch { ChildCount: 1 } branch)
        {
            root = branch[0];
        }

        return root;
    }

    static (Node? Left, Node? Right) Split(Node? node, long index)
    {
        if (index == 0)
        {
            return (null, node);
        }

        if (index == node!.Length)
        {
            return (node, null);
        }

        if (node is Leaf leaf)
        {
            return (new Leaf(leaf.Memory[..(int)index]), new Leaf(leaf.Memory[(int)index..]));
        }

        var branch = (Branch)node;
        for (var i = 0; i < branch.ChildCount; i++)
        {
            if (index == 0)
            {
                return (MakeRoot(branch.Children[..i]), MakeRoot(branch.Children[i..]));
            }

            var child = branch[i];
            if (index < child.Length)
            {
                var (before, after) = Split(child, index);
                return (JoinRoots(MakeRoot(branch.Children[..i]), before),
                    JoinRoots(after, MakeRoot(branch.Children[(i + 1)..])));
            }

            index -= child.Length;
        }

        throw new InvalidOperationException("Split exceeded the cached node length.");
    }

    static Node? MakeRoot(ReadOnlySpan<Node> children) => children.Length switch
    {
        0 => null,
        1 => children[0],
        _ => new Branch(children)
    };

    static Node? JoinRoots(Node? left, Node? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return RootOf(Join(left, right));
    }

    static Changes Join(Node left, Node right)
    {
        if (left is Leaf leftLeaf && right is Leaf rightLeaf)
        {
            return JoinLeaves(leftLeaf, rightLeaf);
        }

        JoinSlots slots = default;
        Span<Node> children = slots;
        if (left.Depth == right.Depth)
        {
            var a = (Branch)left;
            var b = (Branch)right;
            a.Children.CopyTo(children);
            b.Children.CopyTo(children[a.ChildCount..]);
            return Pack(children[..(a.ChildCount + b.ChildCount)]);
        }

        if (left.Depth > right.Depth)
        {
            var branch = (Branch)left;
            var boundary = Join(branch[branch.ChildCount - 1], right);
            branch.Children[..(branch.ChildCount - 1)].CopyTo(children);
            boundary.CopyTo(children[(branch.ChildCount - 1)..]);
            return Pack(children[..(branch.ChildCount - 1 + boundary.Count)]);
        }
        else
        {
            var branch = (Branch)right;
            var boundary = Join(left, branch[0]);
            boundary.CopyTo(children);
            branch.Children[1..].CopyTo(children[boundary.Count..]);
            return Pack(children[..(branch.ChildCount - 1 + boundary.Count)]);
        }
    }

    static Changes JoinLeaves(Leaf left, Leaf right) => left.Length + right.Length <= MergeLeafLength
        ? new(Merge(left.Memory, right.Memory)) : new(left, right);

    static Leaf Merge(ReadOnlyMemory<T> left, ReadOnlyMemory<T> right)
    {
        var buffer = new T[left.Length + right.Length];
        left.Span.CopyTo(buffer);
        right.Span.CopyTo(buffer.AsSpan(left.Length));
        return new(buffer);
    }

    static Changes Pack(ReadOnlySpan<Node> children)
    {
        if (children.Length <= MaximumChildren)
        {
            return new(new Branch(children));
        }

        var half = children.Length / 2;
        return new(new Branch(children[..half]), new Branch(children[half..]));
    }

    static Node RootOf(Changes changes)
    {
        if (changes.Count == 1)
        {
            return changes.First;
        }

        JoinSlots slots = default;
        Span<Node> children = slots;
        changes.CopyTo(children);
        return new Branch(children[..changes.Count]);
    }

    readonly struct Changes(Node first, Node? second = null, Node? third = null)
    {
        internal Node First { get; } = first;
        internal int Count => third is not null ? 3 : second is not null ? 2 : 1;
        internal void CopyTo(Span<Node> destination)
        {
            destination[0] = First;
            if (second is not null)
            {
                destination[1] = second;
            }

            if (third is not null)
            {
                destination[2] = third;
            }
        }
    }

    abstract class Node(TSummary summary, long length, int depth, int bufferCount)
    {
        internal readonly TSummary Summary = summary;
        internal readonly long Length = length;
        internal readonly int Depth = depth;
        internal readonly int BufferCount = bufferCount;
    }

    sealed class Leaf(ReadOnlyMemory<T> memory) : Node(TOps.SummarizeSpan(memory.Span), memory.Length, 0, 1)
    {
        internal readonly ReadOnlyMemory<T> Memory = memory;
    }

    sealed class Branch : Node
    {
        ChildSlots _children;
        internal readonly int ChildCount;
        internal Node this[int index] => _children[index];
        internal ReadOnlySpan<Node> Children => _children[..ChildCount];

        internal Branch(ReadOnlySpan<Node> children) : this(children, Measure(children)) { }
        Branch(ReadOnlySpan<Node> children, Metrics metrics)
            : base(metrics.Summary, metrics.Length, children[0].Depth + 1, metrics.BufferCount)
        {
            ChildCount = children.Length;
            children.CopyTo(_children);
        }
    }

    readonly record struct Metrics(TSummary Summary, long Length, int BufferCount);

    static Metrics Measure(ReadOnlySpan<Node> children)
    {
        var summary = children[0].Summary;
        var length = children[0].Length;
        var buffers = children[0].BufferCount;
        for (var i = 1; i < children.Length; i++)
        {
            var child = children[i];
            summary = TOps.Combine(in summary, in child.Summary);
            length = checked(length + child.Length);
            buffers = checked(buffers + child.BufferCount);
        }

        return new(summary, length, buffers);
    }

    [InlineArray(MaximumChildren)]
    struct ChildSlots { Node _element0; }

    [InlineArray(MaximumChildren * 2)]
    struct JoinSlots { Node _element0; }

    void ValidateIndex(long index, bool allowEnd)
    {
        if (index < 0 || index > Length || (!allowEnd && index == Length))
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    void ValidateRange(long start, long length)
    {
        ValidateIndex(start, allowEnd: true);
        if (length < 0 || length > Length - start)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }

    /// <summary>Checks occupancy, equal leaf depth, cached metadata, and leaf bounds outside hot paths.</summary>
    internal void ValidateStructure()
    {
        if (_root is { } root)
        {
            ValidateNode(root, isRoot: true);
        }
    }

    static void ValidateNode(Node node, bool isRoot)
    {
        if (node is Leaf leaf)
        {
            if (leaf.Memory.Length is < 1 or > ChunkLength || leaf.Depth != 0 || leaf.BufferCount != 1 ||
                leaf.Length != leaf.Memory.Length ||
                !EqualityComparer<TSummary>.Default.Equals(leaf.Summary, TOps.SummarizeSpan(leaf.Memory.Span)))
            {
                throw new InvalidOperationException("Invalid leaf metadata or summary.");
            }

            return;
        }

        var branch = (Branch)node;
        if (branch.ChildCount < (isRoot ? 2 : MinimumChildren) || branch.ChildCount > MaximumChildren)
        {
            throw new InvalidOperationException("Invalid B+ tree occupancy.");
        }

        for (var i = 0; i < branch.ChildCount; i++)
        {
            var child = branch[i];
            if (child.Depth != branch.Depth - 1)
            {
                throw new InvalidOperationException("Unequal leaf depths.");
            }

            ValidateNode(child, isRoot: false);
        }

        var measured = Measure(branch.Children);
        if (branch.Length != measured.Length || branch.BufferCount != measured.BufferCount ||
            !EqualityComparer<TSummary>.Default.Equals(branch.Summary, measured.Summary))
        {
            throw new InvalidOperationException("Invalid internal metadata or summary.");
        }
    }
}
