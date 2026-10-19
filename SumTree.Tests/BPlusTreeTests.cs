using CountTreeChar = SumTree.SumTree<char, long, SumTree.Tests.CountOperations>;
using TextTreeChar = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace SumTree.Tests;
public class BPlusTreeTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(1024)]
    [Arguments(1025)]
    [Arguments(8193)]
    [Arguments(65537)]
    [Arguments(2097152)]
    public async Task BulkConstructionMaintainsOccupancyAndMinimumHeight(int length)
    {
        var source = new string('a', length);
        var tree = new TextTreeChar(source.AsMemory());
        await Check(tree, source);
        var leaves = (length + TextTreeChar.ChunkLength - 1) / TextTreeChar.ChunkLength;
        var height = 0;
        var capacity = 1L;
        while (capacity < leaves)
        {
            capacity *= TextTreeChar.MaximumChildren;
            height++;
        }
        await Assert.That(tree.Depth).IsEqualTo(height);
        await Assert.That(tree.BufferCount).IsEqualTo(leaves);
    }

    [Test]
    public async Task SplitsAndUnequalHeightJoinsRepairUnderfullRootsLocally()
    {
        var source = string.Concat(Enumerable.Repeat("Call(items[0]);\r\n", 6000));
        var tree = new TextTreeChar(source.AsMemory());
        var small = new TextTreeChar("{\r\n}".AsMemory());
        var boundaries = new[] { 0, 1, 1023, 1024, 1025, 4096, 8193, source.Length / 2,
            source.Length - 1024, source.Length - 1, source.Length };
        foreach (var at in boundaries)
        {
            var (left, right) = tree.SplitAt(at);
            await Check(left, source[..at]);
            await Check(right, source[at..]);
            await Check(left + right, source);
            await Check(left + small + right, source.Insert(at, "{\r\n}"));
            await Check(small + right + left + small, "{\r\n}" + source[at..] + source[..at] + "{\r\n}");
        }
        await Check(tree, source);
    }

    [Test]
    public async Task ThreeWayLeafSplitsPropagateThroughFullParents()
    {
        var source = new string('a', TextTreeChar.MaximumChildren * TextTreeChar.ChunkLength);
        var inserted = new string('b', TextTreeChar.ChunkLength);
        var tree = new TextTreeChar(source.AsMemory());
        var held = tree;
        for (var i = 0; i < 80; i++)
        {
            var at = 513 + i * 101;
            tree = tree.InsertRange(at, inserted.AsMemory());
            source = source.Insert(at, inserted);
            await Check(tree, source);
        }
        await Check(held, new string('a', TextTreeChar.MaximumChildren * TextTreeChar.ChunkLength));
    }

    [Test]
    public async Task RandomBulkEditsSplitsAndRootCollapseMatchReference()
    {
        var text = string.Concat(Enumerable.Repeat("({items[0]})\r\n", 5000));
        var tree = new TextTreeChar(text.AsMemory());
        var random = new Random(7321);
        var held = new List<(TextTreeChar Tree, string Text)>();
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
                var prepared = new TextTreeChar(insert.AsMemory());
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
            await Check(tree, text);
        }
        foreach (var snapshot in held)
        {
            await Check(snapshot.Tree, snapshot.Text);
        }

        var empty = tree.RemoveRange(0, tree.Length);
        await Check(empty, "");
        await Check(empty.Add('x'), "x");
    }

    [NotInParallel("CountOperations")]
    [Test]
    public async Task DeletingFullLeavesRedistributesMergesAndCollapsesTheRoot()
    {
        var leafCount = CountTreeChar.MaximumChildren * CountTreeChar.MaximumChildren * CountTreeChar.MaximumChildren;
        var tree = new CountTreeChar(new string('x', leafCount * CountTreeChar.ChunkLength).AsMemory());
        var snapshots = new List<(CountTreeChar Tree, long Length)>();
        await Assert.That(tree.Depth).IsEqualTo(3);
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
            tree = tree.RemoveRange((long)index * CountTreeChar.ChunkLength, CountTreeChar.ChunkLength);
            tree.ValidateStructure();
            await Assert.That(tree.Length).IsEqualTo((long)(remaining - 1) * CountTreeChar.ChunkLength);
            await Assert.That(tree.Summary).IsEqualTo(tree.Length);
        }
        await Assert.That(tree.IsEmpty).IsTrue();
        await Assert.That(tree.Depth).IsEqualTo(0);
        foreach (var snapshot in snapshots)
        {
            snapshot.Tree.ValidateStructure();
            await Assert.That(snapshot.Tree.Summary).IsEqualTo(snapshot.Length);
        }
    }

    [Test]
    public async Task BackspaceAtALeafBoundaryCopiesOnlyOnePath()
    {
        var tree = new TextTreeChar(new string('a', 2_097_152).AsMemory());
        _ = tree.RemoveAt(1_048_575);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var edited = tree.RemoveAt(1_048_575);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsLessThan(2048).Because($"Backspace allocated {allocated} bytes");
        edited.ValidateStructure();
        await Assert.That(edited.Length).IsEqualTo(2_097_151L);
        await Assert.That(tree.Length).IsEqualTo(2_097_152L);
    }

    [NotInParallel("CountOperations")]
    [Test]
    [Arguments(4096)]
    [Arguments(131072)]
    [Arguments(2097152)]
    [Arguments(16777216)]
    public async Task PointEditsAndAppendSplitsUseBoundedWork(int length)
    {
        var source = new string('a', length);
        var tree = new CountTreeChar(source.AsMemory());
        var before = tree;
        var budget = 8L * CountTreeChar.MaximumChildren * (tree.Depth + 2) * (tree.Depth + 2);
        CountOperations.Reset();
        var inserted = tree.InsertRange(length / 2 + 17, "edit\n".AsMemory());
        await Assert.That(CountOperations.Combines).IsLessThanOrEqualTo(budget).Because($"Insertion combined {CountOperations.Combines} summaries");
        await Assert.That(CountOperations.Scanned).IsLessThanOrEqualTo(2 * CountTreeChar.ChunkLength + 10);
        inserted.ValidateStructure();
        await Assert.That(inserted.Summary).IsEqualTo((long)length + 5);

        CountOperations.Reset();
        var deleted = inserted.RemoveRange(length / 2 + 17, 5);
        await Assert.That(CountOperations.Combines).IsLessThanOrEqualTo(budget).Because($"Deletion combined {CountOperations.Combines} summaries");
        await Assert.That(CountOperations.Scanned).IsLessThanOrEqualTo(4 * CountTreeChar.ChunkLength);
        deleted.ValidateStructure();
        await Assert.That(deleted.Summary).IsEqualTo(length);

        var fragment = new CountTreeChar(new string('b', CountTreeChar.ChunkLength).AsMemory());
        for (var i = 0; i < 80; i++)
        {
            CountOperations.Reset();
            tree += fragment;
            await Assert.That(CountOperations.Combines).IsLessThanOrEqualTo(budget).Because($"Append combined {CountOperations.Combines} summaries");
            await Assert.That(CountOperations.Scanned).IsEqualTo(0L).Because("Appending full prepared leaves must not rescan text");
        }
        tree.ValidateStructure();
        before.ValidateStructure();
        await Assert.That(before.Summary).IsEqualTo(length);
        await Assert.That(tree.Summary).IsEqualTo((long)length + 80 * CountTreeChar.ChunkLength);
    }

    static async Task Check(TextTreeChar tree, string text)
    {
        tree.ValidateStructure();
        await Assert.That(tree.ToString()).IsEqualTo(text);
        await Assert.That(tree.Length).IsEqualTo(text.Length);
        await Assert.That(tree.Summary).IsEqualTo(TextSummaryOperations.SummarizeSpan(text));
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