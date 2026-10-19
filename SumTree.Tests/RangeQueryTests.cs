using TextTreeChar = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace SumTree.Tests;
public class RangeQueryTests
{
    [Test]
    public async Task RangesAndTraversalMatchLinearReferenceAfterEdits()
    {
        var random = new Random(721);
        var text = string.Concat(Enumerable.Repeat("alpha\r\n({x})\rbeta\n", 2000));
        var tree = new TextTreeChar(text.AsMemory());
        for (var edit = 0; edit < 100; edit++)
        {
            var offset = random.Next(text.Length + 1);
            text = text.Insert(offset, "\r\n{}");
            tree = tree.InsertRange(offset, "\r\n{}".AsMemory());
        }
        for (var i = 0; i < 1000; i++)
        {
            var start = random.Next(text.Length + 1);
            var length = random.Next(text.Length - start + 1);
            var expected = TextSummaryOperations.SummarizeSpan(text.AsSpan(start, length));
            await Assert.That(tree.GetRangeSummary(start, length)).IsEqualTo(expected);
            await Assert.That(tree.GetPrefixSummary(start)).IsEqualTo(TextSummaryOperations.SummarizeSpan(text.AsSpan(0, start)));
            var output = new char[length];
            tree.CopyTo(start, output);
            await Assert.That(new string(output)).IsEqualTo(text.Substring(start, length));
        }
        var builder = new StringBuilder();
        foreach (var chunk in tree.GetChunks())
        {
            builder.Append(chunk);
        }

        await Assert.That(builder.ToString()).IsEqualTo(text);
        builder.Clear();
        foreach (var item in tree) { builder.Append(item); }
        await Assert.That(builder.ToString()).IsEqualTo(text);
        await Assert.That(new string([.. tree])).IsEqualTo(text);
        await Assert.That(() => tree.GetRangeSummary(-1, 0)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => tree.GetRangeSummary(0, tree.Length + 1)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(default(TextTreeChar).GetPrefixSummary(0)).IsEqualTo(TextSummaryOperations.Identity);
    }

    [Test]
    [Arguments("")]
    [Arguments("a\r\nb\rc\n\n")]
    [Arguments("\r\n\r\n")]
    [Arguments("last")]
    public async Task LineHelpersHandleEveryLineAndEndpoint(string suffix)
    {
        // The CRLF straddles the initial 1024-element leaf boundary.
        var text = new string('a', 1023) + "\r\n" + suffix;
        var tree = new TextTreeChar(text.AsMemory());
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var start = 0;
        for (var line = 1; line <= lines.Length; line++)
        {
            await Assert.That(tree.FindLineStart(line)).IsEqualTo(start);
            await Assert.That(tree.GetLineContent(line)).IsEqualTo(lines[line - 1]);
            start += lines[line - 1].Length;
            if (start < text.Length)
            {
                start += text[start] == '\r' && start + 1 < text.Length && text[start + 1] == '\n' ? 2 : 1;
            }
        }
        await Assert.That(tree.FindLineStart(lines.Length + 1)).IsEqualTo(-1L);
        await Assert.That(tree.GetLineContent(lines.Length + 1)).IsNull();
        for (var i = 0; i <= text.Length; i++)
        {
            var summary = LineNumberSummary.SummarizeSpan(text.AsSpan(0, i));
            var expected = (summary.Lines + 1, summary.LastLineCharacters + 1);
            await Assert.That(tree.GetLineAndColumn(i)).IsEqualTo(expected);
        }
        await Assert.That(default(TextTreeChar).GetLineContent(1)).IsEqualTo("");
        await Assert.That(() => tree.FindLineStart(0)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [NotInParallel("CountOperations")]
    [Test]
    public async Task RangeQueriesScanOnlyBoundaryLeavesAndReuseFullSummaries()
    {
        var tree = new SumTree<char, long, CountOperations>(new string('a', 2_097_152).AsMemory());
        CountOperations.Reset();
        await Assert.That(tree.GetRangeSummary(0, tree.Length)).IsEqualTo(tree.Length);
        await Assert.That(CountOperations.Scanned).IsEqualTo(0L);
        await Assert.That(CountOperations.Combines).IsEqualTo(0L);
        await Assert.That(tree.GetRangeSummary(17, 1_000_003)).IsEqualTo(1_000_003L);
        await Assert.That(CountOperations.Scanned).IsLessThanOrEqualTo(2048);
        await Assert.That(CountOperations.Combines).IsLessThanOrEqualTo(2 * tree.Depth * 8);
    }

    [Test]
    public async Task TypedQueriesAndConcreteTraversalDoNotAllocate()
    {
        var tree = new TextTreeChar(new string('x', 2_097_152).AsMemory());
        long Visit()
        {
            long sum = 0;
            foreach (var item in tree) { sum += item; }
            foreach (var chunk in tree.GetChunks(1000, 10000)) { sum += chunk.Length; }
            for (var i = 0; i < 100; i++)
            {
                sum += tree.GetRangeSummary(i * 1000, 5000).Lines.TotalCharacters;
            }
            return sum;
        }
        Visit();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = Visit();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(result).IsGreaterThan(0);
        await Assert.That(allocated).IsEqualTo(0L);
    }
}