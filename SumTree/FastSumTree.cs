using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SumTree;

/// <summary>
/// High-performance FastSumTree implementation optimized for minimal allocations.
/// Uses compile-time generics to eliminate dictionary overhead and reflection.
/// </summary>
/// <typeparam name="T">The element type</typeparam>
public readonly struct FastSumTree<T> : IEnumerable<T>, IReadOnlyList<T>, IEquatable<FastSumTree<T>>
    where T : IEquatable<T>
{
    private const int MaxTreeDepth = 46;
    private const int MaxDepthImbalance = 4;
    private const int LargeObjectHeapBytes = 85_000 - 24;

    [ThreadStatic]
    private static int t_balancedCallDepth;

    public static readonly int MaxLeafLength = CalculateMaxLeafLength();
    public static readonly FastSumTree<T> Empty = new();

    private static readonly int[] s_fibonacciPlusTwo =
        Enumerable.Range(0, MaxTreeDepth).Select(d => Fibonacci(d) + 2).ToArray();

    private readonly object _data;
    private readonly long _length;
    private readonly int _depth;
    private readonly int _bufferCount;
    private readonly bool _isBalanced;

    /// <summary>
    /// Creates empty FastFastSumTree
    /// </summary>
    public FastSumTree()
    {
        _data = ReadOnlyMemory<T>.Empty;
        _length = 0;
        _depth = 0;
        _bufferCount = 1;
        _isBalanced = true;
    }

    /// <summary>
    /// Creates FastFastSumTree with single value
    /// </summary>
    public FastSumTree(T value)
    {
        _data = ValueTuple.Create(value);
        _length = 1;
        _depth = 0;
        _bufferCount = 1;
        _isBalanced = true;
    }

    /// <summary>
    /// Creates FastFastSumTree from memory
    /// </summary>
    public FastSumTree(ReadOnlyMemory<T> memory)
    {
        if (memory.IsEmpty)
        {
            this = Empty;
            return;
        }

        _data = memory;
        _length = memory.Length;
        _depth = 0;
        _bufferCount = 1;
        _isBalanced = true;
    }

    /// <summary>
    /// Internal constructor for nodes
    /// </summary>
    private FastSumTree(object data, long length, int depth, int bufferCount, bool isBalanced)
    {
        _data = data;
        _length = length;
        _depth = depth;
        _bufferCount = bufferCount;
        _isBalanced = isBalanced;
    }

    public long Length => _length;
    public int Count => (int)Math.Min(_length, int.MaxValue);
    public bool IsEmpty => _length == 0;
    public bool IsNode => _data is ValueTuple<FastSumTree<T>, FastSumTree<T>>;
    public bool IsBalanced => _isBalanced;
    public int Depth => _depth;
    public int BufferCount => _bufferCount;

    private FastSumTree<T> Left =>
        IsNode ? ((ValueTuple<FastSumTree<T>, FastSumTree<T>>)_data).Item1 :
        throw new InvalidOperationException("Not a node");

    private FastSumTree<T> Right =>
        IsNode ? ((ValueTuple<FastSumTree<T>, FastSumTree<T>>)_data).Item2 :
        throw new InvalidOperationException("Not a node");

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FastSumTree<T> Add(T item)
    {
        var itemTree = new FastSumTree<T>(item);
        return AddRange(itemTree);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FastSumTree<T> AddRange(FastSumTree<T> other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        return Concat(this, other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FastSumTree<T> AddRange(ReadOnlyMemory<T> items)
    {
        if (items.IsEmpty) return this;
        return AddRange(new FastSumTree<T>(items));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FastSumTree<T> AddRange(T[] items)
    {
        return AddRange(items.AsMemory());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FastSumTree<T> AddRange(IEnumerable<T> items)
    {
        if (items is T[] array)
            return AddRange(array);

        if (items is List<T> list)
            return AddRange(CollectionsMarshal.AsSpan(list));

        return AddRange(items.ToArray());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FastSumTree<T> AddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty) return this;
        // Need to copy span to memory since span is stack-only
        var array = items.ToArray();
        return AddRange(new FastSumTree<T>(array.AsMemory()));
    }

    public FastSumTree<T> Insert(long index, T item)
    {
        var itemTree = new FastSumTree<T>(item);
        return InsertRange(index, itemTree);
    }

    public FastSumTree<T> InsertRange(long index, FastSumTree<T> other)
    {
        if (index == 0) return other.AddRange(this);
        if (index == _length) return AddRange(other);

        var (left, right) = SplitAt(index);
        return Concat(Concat(left, other), right);
    }

    public (FastSumTree<T> Left, FastSumTree<T> Right) SplitAt(long index)
    {
        if (index <= 0) return (Empty, this);
        if (index >= _length) return (this, Empty);

        return SplitAtInternal(index);
    }

    private (FastSumTree<T> Left, FastSumTree<T> Right) SplitAtInternal(long index)
    {
        var current = this;
        var currentIndex = index;
        var leftParts = new List<FastSumTree<T>>();
        var rightParts = new List<FastSumTree<T>>();

        while (current.IsNode)
        {
            var leftLength = current.Left._length;
            if (currentIndex <= leftLength)
            {
                rightParts.Add(current.Right);
                current = current.Left;
            }
            else
            {
                leftParts.Add(current.Left);
                current = current.Right;
                currentIndex -= leftLength;
            }
        }

        var (leafLeft, leafRight) = current._data switch
        {
            ReadOnlyMemory<T> memory => SplitMemory(memory, (int)currentIndex),
            ValueTuple<T> single => currentIndex == 0 ? (Empty, current) : (current, Empty),
            _ => throw new InvalidOperationException("Invalid data type")
        };

        var left = leafLeft;
        for (var i = leftParts.Count - 1; i >= 0; i--)
        {
            left = Concat(leftParts[i], left);
        }

        var right = leafRight;
        for (var i = rightParts.Count - 1; i >= 0; i--)
        {
            right = Concat(right, rightParts[i]);
        }

        return (left, right);
    }

    private (FastSumTree<T> Left, FastSumTree<T> Right) SplitMemory(ReadOnlyMemory<T> memory, int index)
    {
        if (index == 0) return (Empty, new FastSumTree<T>(memory));
        if (index >= memory.Length) return (new FastSumTree<T>(memory), Empty);

        var leftMemory = memory[..index];
        var rightMemory = memory[index..];

        return (
            new FastSumTree<T>(leftMemory),
            new FastSumTree<T>(rightMemory)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FastSumTree<T> Concat(FastSumTree<T> left, FastSumTree<T> right)
    {
        if (left.IsEmpty) return right;
        if (right.IsEmpty) return left;

        var newLength = left._length + right._length;
        var newDepth = Math.Max(left._depth, right._depth) + 1;
        var newBufferCount = left._bufferCount + right._bufferCount;
        var isBalanced = CalculateIsBalanced(newLength, newDepth);

        var result = new FastSumTree<T>(
            new ValueTuple<FastSumTree<T>, FastSumTree<T>>(left, right),
            newLength,
            newDepth,
            newBufferCount,
            isBalanced
        );

        return isBalanced ? result : result.Balanced();
    }

    private FastSumTree<T> Balanced()
    {
        if (_isBalanced) return this;

        t_balancedCallDepth++;
        try
        {
            if (t_balancedCallDepth > MaxTreeDepth)
                throw new InvalidOperationException("Tree depth exceeded maximum");

            var leaves = new List<FastSumTree<T>>();
            CollectLeaves(leaves);
            return CombineLeaves(leaves);
        }
        finally
        {
            t_balancedCallDepth--;
        }
    }

    private void CollectLeaves(List<FastSumTree<T>> leaves)
    {
        if (!IsNode)
        {
            leaves.Add(this);
            return;
        }

        Left.CollectLeaves(leaves);
        Right.CollectLeaves(leaves);
    }

    private static FastSumTree<T> CombineLeaves(List<FastSumTree<T>> leaves)
    {
        while (leaves.Count > 1)
        {
            var newLeaves = new List<FastSumTree<T>>();

            for (var i = 0; i < leaves.Count; i += 2)
            {
                if (i + 1 < leaves.Count)
                {
                    newLeaves.Add(Concat(leaves[i], leaves[i + 1]));
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

        ((FastSumTree<char>)(object)Left).CopyToChar(destination, ref index);
        ((FastSumTree<char>)(object)Right).CopyToChar(destination, ref index);
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

        ((FastSumTree<char>)(object)Left).AppendToStringBuilder(sb);
        ((FastSumTree<char>)(object)Right).AppendToStringBuilder(sb);
    }

    public bool Equals(FastSumTree<T> other)
    {
        if (_length != other._length) return false;
        if (_length == 0) return true;

        var thisEnum = EnumerateElements();
        var otherEnum = other.EnumerateElements();

        return thisEnum.SequenceEqual(otherEnum);
    }

    public override bool Equals(object? obj) => obj is FastSumTree<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_length);

        foreach (var item in EnumerateElements())
            hash.Add(item);

        return hash.ToHashCode();
    }

    public static bool operator ==(FastSumTree<T> left, FastSumTree<T> right) => left.Equals(right);
    public static bool operator !=(FastSumTree<T> left, FastSumTree<T> right) => !left.Equals(right);
}
