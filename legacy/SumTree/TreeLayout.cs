namespace SumTree;

/// <summary>
/// Depth limits, balancing tables, and recursion guards shared by every closed tree type.
/// </summary>
/// <remarks>
/// None of these values depend on the element type, so they live here instead of inside
/// <c>SumTree&lt;T&gt;</c> and its typed variant, where the runtime keeps a separate
/// copy of every static field for each closed generic type.
/// </remarks>
static class TreeLayout
{
    /// <summary>
    /// Maximum tree depth allowed.
    /// </summary>
    internal const int MaxTreeDepth = 46;

    /// <summary>
    /// Maximum number of bytes before the GC puts buffers on the large object heap.
    /// </summary>
    internal const int LargeObjectHeapBytes = 85_000 - 24;

    /// <summary>
    /// Defines the minimum lengths the leaves should be in relation to the depth of the tree.
    /// </summary>
    internal static readonly int[] FibonacciPlusTwo =
        [.. Enumerable.Range(0, MaxTreeDepth).Select(d => Fibonacci(d) + 2)];

    /// <summary>
    /// Per-thread count of nested <c>Balanced()</c> calls, shared across element types.
    /// </summary>
    [ThreadStatic]
    internal static int BalancedCallDepth;

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
}
