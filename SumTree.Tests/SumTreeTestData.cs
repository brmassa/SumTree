using System;
using System.Linq;

namespace SumTree.Tests;

internal class SumTreeTestData
{
    internal static readonly SumTree<int> EvenNumbers = Enumerable.Range(0, 2048).Where(i => i % 2 == 0).ToSumTree();

    internal static readonly SumTree<char> LargeText = Enumerable.Range(0, 32 * 1024).Select(i => (char)(65 + (i % 26))).ToSumTree();

    internal static (string, SumTree<char>) Create(int length, int chunkSize)
    {
        if (length == 0)
        {
            return (string.Empty, SumTree<char>.Empty);
        }

        var chars = Enumerable.Range(0, length).Select(i => (char)(65 + (i % 26))).ToArray();
        var expected = new string(chars);

        // Create SumTree directly from the array to avoid balancing issues
        var sumTree = new SumTree<char>(chars.AsMemory());
        return (expected, sumTree);
    }
}
