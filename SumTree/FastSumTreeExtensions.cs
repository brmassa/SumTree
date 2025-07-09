using System.Runtime.CompilerServices;

namespace SumTree;

/// <summary>
/// Extension methods for FastSumTree
/// </summary>
public static class FastSumTreeExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FastSumTree<char> ToFastSumTree(this string text)
    {
        return text.Length == 0 ? FastSumTree<char>.Empty : new FastSumTree<char>(text.AsMemory());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FastSumTree<T> ToFastSumTree<T>(this ReadOnlyMemory<T> memory)
        where T : IEquatable<T>
    {
        return memory.IsEmpty ? FastSumTree<T>.Empty : new FastSumTree<T>(memory);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FastSumTree<T> ToFastSumTree<T>(this T[] array)
        where T : IEquatable<T>
    {
        return array.Length == 0 ? FastSumTree<T>.Empty : new FastSumTree<T>(array.AsMemory());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FastSumTree<T> ToFastSumTree<T>(this IEnumerable<T> enumerable)
        where T : IEquatable<T>
    {
        if (enumerable is T[] array)
            return array.ToFastSumTree();

        if (enumerable is List<T> list)
            return list.ToArray().ToFastSumTree();

        var items = enumerable.ToArray();
        return items.ToFastSumTree();
    }

    public static FastSumTree<T> RemoveRange<T>(this FastSumTree<T> tree, long start, long length)
        where T : IEquatable<T>
    {
        if (length <= 0) return tree;
        if (start < 0 || start >= tree.Length) return tree;

        var actualLength = Math.Min(length, tree.Length - start);
        var end = start + actualLength;

        if (start == 0 && end >= tree.Length)
            return FastSumTree<T>.Empty;

        if (start == 0)
        {
            var (_, right) = tree.SplitAt(end);
            return right;
        }

        if (end >= tree.Length)
        {
            var (left, _) = tree.SplitAt(start);
            return left;
        }

        var (leftPart, rest) = tree.SplitAt(start);
        var (_, rightPart) = rest.SplitAt(actualLength);
        return leftPart.AddRange(rightPart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FastSumTree<T> RemoveAt<T>(this FastSumTree<T> tree, long index)
        where T : IEquatable<T>
    {
        return tree.RemoveRange(index, 1);
    }

    public static FastSumTree<T> Slice<T>(this FastSumTree<T> tree, long start, long length)
        where T : IEquatable<T>
    {
        if (length <= 0) return FastSumTree<T>.Empty;
        if (start < 0 || start >= tree.Length) return FastSumTree<T>.Empty;

        var actualLength = Math.Min(length, tree.Length - start);
        var end = start + actualLength;

        if (start == 0 && end >= tree.Length)
            return tree;

        if (start == 0)
        {
            var (left, _) = tree.SplitAt(end);
            return left;
        }

        if (end >= tree.Length)
        {
            var (_, right) = tree.SplitAt(start);
            return right;
        }

        var (_, rest) = tree.SplitAt(start);
        var (middle, _) = rest.SplitAt(actualLength);
        return middle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FastSumTree<T> Slice<T>(this FastSumTree<T> tree, long start)
        where T : IEquatable<T>
    {
        if (start <= 0) return tree;
        if (start >= tree.Length) return FastSumTree<T>.Empty;

        var (_, right) = tree.SplitAt(start);
        return right;
    }
}
