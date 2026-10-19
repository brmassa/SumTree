using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CountTree = SumTree.SumTree<char, long, SumTree.Tests.CountOperations>;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace SumTree.Tests;

[TestClass]
public class BPlusTreeTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(1024)]
    [DataRow(1025)]
    [DataRow(8193)]
    [DataRow(65537)]
    [DataRow(2097152)]
    public void BulkConstructionMaintainsOccupancyAndMinimumHeight(int length)
    {
        var source = new string('a', length);
        var tree = new TextTree(source.AsMemory());
        Check(tree, source);
        var leaves = (length + TextTree.ChunkLength - 1) / TextTree.ChunkLength;
        var height = 0;
        var capacity = 1L;
        while (capacity < leaves)
        {
            capacity *= TextTree.MaximumChildren;
            height++;
        }
        Assert.AreEqual(height, tree.Depth);
        Assert.AreEqual(leaves, tree.BufferCount);
    }

    [TestMethod]
    public void SplitsAndUnequalHeightJoinsRepairUnderfullRootsLocally()
    {
        var source = string.Concat(Enumerable.Repeat("Call(items[0]);\r\n", 6000));
        var tree = new TextTree(source.AsMemory());
        var small = new TextTree("{\r\n}".AsMemory());
        var boundaries = new[] { 0, 1, 1023, 1024, 1025, 4096, 8193, source.Length / 2,
            source.Length - 1024, source.Length - 1, source.Length };
        foreach (var at in boundaries)
        {
            var (left, right) = tree.SplitAt(at);
            Check(left, source[..at]);
            Check(right, source[at..]);
            Check(left + right, source);
            Check(left + small + right, source.Insert(at, "{\r\n}"));
            Check(small + right + left + small, "{\r\n}" + source[at..] + source[..at] + "{\r\n}");
        }
        Check(tree, source);
    }

    [TestMethod]
    public void ThreeWayLeafSplitsPropagateThroughFullParents()
    {
        var source = new string('a', TextTree.MaximumChildren * TextTree.ChunkLength);
        var inserted = new string('b', TextTree.ChunkLength);
        var tree = new TextTree(source.AsMemory());
        var held = tree;
        for (var i = 0; i < 80; i++)
        {
            var at = 513 + i * 101;
            tree = tree.InsertRange(at, inserted.AsMemory());
            source = source.Insert(at, inserted);
            Check(tree, source);
        }
        Check(held, new string('a', TextTree.MaximumChildren * TextTree.ChunkLength));
    }

    [TestMethod]
    public void RandomBulkEditsSplitsAndRootCollapseMatchReference()
    {
        var text = string.Concat(Enumerable.Repeat("({items[0]})\r\n", 5000));
        var tree = new TextTree(text.AsMemory());
        var random = new Random(7321);
        var held = new List<(TextTree Tree, string Text)>();
        for (var edit = 0; edit < 700; edit++)
        {
            if (edit % 73 == 0)
            {
                held.Add((tree, text));
            }

            var at = random.Next(text.Length + 1);
            if (edit % 4 == 0)
            {
                var count = random.Next(text.Length - at + 1);
                tree = tree.RemoveRange(at, count);
                text = text.Remove(at, count);
            }
            else
            {
                var insert = new string(edit % 2 == 0 ? '\r' : '\n', random.Next(2500));
                var prepared = new TextTree(insert.AsMemory());
                if (edit % 4 == 1)
                {
                    var (left, right) = tree.SplitAt(at);
                    tree = left + prepared + right;
                }
                else
                {
                    tree = tree.InsertRange(at, prepared);
                }

                text = text.Insert(at, insert);
            }
            Check(tree, text);
        }
        foreach (var snapshot in held)
        {
            Check(snapshot.Tree, snapshot.Text);
        }

        var empty = tree.RemoveRange(0, tree.Length);
        Check(empty, "");
        Check(empty.Add('x'), "x");
    }

    [TestMethod]
    public void DeletingFullLeavesRedistributesMergesAndCollapsesTheRoot()
    {
        var leafCount = CountTree.MaximumChildren * CountTree.MaximumChildren * CountTree.MaximumChildren;
        var tree = new CountTree(new string('x', leafCount * CountTree.ChunkLength).AsMemory());
        var snapshots = new List<(CountTree Tree, long Length)>();
        Assert.AreEqual(3, tree.Depth);
        for (var remaining = leafCount; remaining > 0; remaining--)
        {
            if (remaining % 97 == 0)
            {
                snapshots.Add((tree, tree.Length));
            }

            var index = (remaining % 3) switch
            {
                0 => 0,
                1 => remaining - 1,
                _ => remaining / 2
            };
            tree = tree.RemoveRange((long)index * CountTree.ChunkLength, CountTree.ChunkLength);
            tree.ValidateStructure();
            Assert.AreEqual((long)(remaining - 1) * CountTree.ChunkLength, tree.Length);
            Assert.AreEqual(tree.Length, tree.Summary);
        }
        Assert.IsTrue(tree.IsEmpty);
        Assert.AreEqual(0, tree.Depth);
        foreach (var snapshot in snapshots)
        {
            snapshot.Tree.ValidateStructure();
            Assert.AreEqual(snapshot.Length, snapshot.Tree.Summary);
        }
    }

    [TestMethod]
    public void BackspaceAtALeafBoundaryCopiesOnlyOnePath()
    {
        var tree = new TextTree(new string('a', 2_097_152).AsMemory());
        _ = tree.RemoveAt(1_048_575);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var edited = tree.RemoveAt(1_048_575);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsLessThan(2048, allocated, $"Backspace allocated {allocated} bytes");
        edited.ValidateStructure();
        Assert.AreEqual(2_097_151L, edited.Length);
        Assert.AreEqual(2_097_152L, tree.Length);
    }

    [TestMethod]
    [DataRow(4096)]
    [DataRow(131072)]
    [DataRow(2097152)]
    [DataRow(16777216)]
    public void PointEditsAndAppendSplitsUseBoundedWork(int length)
    {
        var source = new string('a', length);
        var tree = new CountTree(source.AsMemory());
        var before = tree;
        var budget = 8L * CountTree.MaximumChildren * (tree.Depth + 2) * (tree.Depth + 2);
        CountOperations.Reset();
        var inserted = tree.InsertRange(length / 2 + 17, "edit\n".AsMemory());
        Assert.IsLessThanOrEqualTo(budget, CountOperations.Combines, $"Insertion combined {CountOperations.Combines} summaries");
        Assert.IsLessThanOrEqualTo(2 * CountTree.ChunkLength + 10, CountOperations.Scanned);
        inserted.ValidateStructure();
        Assert.AreEqual((long)length + 5, inserted.Summary);

        CountOperations.Reset();
        var deleted = inserted.RemoveRange(length / 2 + 17, 5);
        Assert.IsLessThanOrEqualTo(budget, CountOperations.Combines, $"Deletion combined {CountOperations.Combines} summaries");
        Assert.IsLessThanOrEqualTo(4 * CountTree.ChunkLength, CountOperations.Scanned);
        deleted.ValidateStructure();
        Assert.AreEqual(length, deleted.Summary);

        var fragment = new CountTree(new string('b', CountTree.ChunkLength).AsMemory());
        for (var i = 0; i < 80; i++)
        {
            CountOperations.Reset();
            tree += fragment;
            Assert.IsLessThanOrEqualTo(budget, CountOperations.Combines, $"Append combined {CountOperations.Combines} summaries");
            Assert.AreEqual(0L, CountOperations.Scanned, "Appending full prepared leaves must not rescan text");
        }
        tree.ValidateStructure();
        before.ValidateStructure();
        Assert.AreEqual(length, before.Summary);
        Assert.AreEqual((long)length + 80 * CountTree.ChunkLength, tree.Summary);
    }

    static void Check(TextTree tree, string text)
    {
        tree.ValidateStructure();
        Assert.AreEqual(text, tree.ToString());
        Assert.AreEqual(text.Length, tree.Length);
        Assert.AreEqual(TextSummaryOperations.SummarizeSpan(text), tree.Summary);
    }
}

// Instrumentation is confined to tests; real operations must remain pure.
readonly struct CountOperations : ISummaryOperations<char, long>
{
    internal static long Combines;
    internal static long Scanned;
    internal static void Reset() { Combines = 0; Scanned = 0; }
    public static long Identity => 0;
    public static long SummarizeElement(char element) { Scanned++; return 1; }
    public static long SummarizeSpan(ReadOnlySpan<char> elements) { Scanned += elements.Length; return elements.Length; }
    public static long Combine(in long left, in long right) { Combines++; return left + right; }
}
