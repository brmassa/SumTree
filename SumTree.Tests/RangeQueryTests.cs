using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace SumTree.Tests;

[TestClass]
public class RangeQueryTests
{
    [TestMethod]
    public void RangesAndTraversalMatchLinearReferenceAfterEdits()
    {
        var random = new Random(721);
        var text = string.Concat(Enumerable.Repeat("alpha\r\n({x})\rbeta\n", 2000));
        var tree = new TextTree(text.AsMemory());
        for (var edit = 0; edit < 100; edit++)
        {
            var offset = random.Next(text.Length + 1);
            text = text.Insert(offset, "\r\n{}");
            tree = tree.InsertRange(offset, "\r\n{}".AsMemory());
        }
        var legacy = text.ToSumTreeWithLinesAndBrackets();
        for (var i = 0; i < 1000; i++)
        {
            var start = random.Next(text.Length + 1);
            var length = random.Next(text.Length - start + 1);
            var expected = TextSummaryOperations.SummarizeSpan(text.AsSpan(start, length));
            Assert.AreEqual(expected, tree.GetRangeSummary(start, length));
            Assert.AreEqual(expected.Lines, legacy.GetRangeSummary(start, length, LineNumberDimension.Instance));
            Assert.AreEqual(TextSummaryOperations.SummarizeSpan(text.AsSpan(0, start)), tree.GetPrefixSummary(start));
            var output = new char[length];
            tree.CopyTo(start, output);
            Assert.AreEqual(text.Substring(start, length), new string(output));
            legacy.CopyTo(start, output);
            Assert.AreEqual(text.Substring(start, length), new string(output));
        }
        var builder = new StringBuilder();
        foreach (var chunk in tree.GetChunks())
        {
            builder.Append(chunk);
        }

        Assert.AreEqual(text, builder.ToString());
        builder.Clear();
        foreach (var item in tree) { builder.Append(item); }
        Assert.AreEqual(text, builder.ToString());
        Assert.AreEqual(text, new string([.. tree]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.GetRangeSummary(-1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.GetRangeSummary(0, tree.Length + 1));
        Assert.AreEqual(TextSummaryOperations.Identity, default(TextTree).GetPrefixSummary(0));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("a\r\nb\rc\n\n")]
    [DataRow("\r\n\r\n")]
    [DataRow("last")]
    public void LineHelpersHandleEveryLineAndEndpoint(string suffix)
    {
        // The CRLF straddles the initial 1024-element leaf boundary.
        var text = new string('a', 1023) + "\r\n" + suffix;
        var tree = new TextTree(text.AsMemory());
        var legacy = text.ToSumTreeWithLines();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var start = 0;
        for (var line = 1; line <= lines.Length; line++)
        {
            Assert.AreEqual(start, tree.FindLineStart(line));
            Assert.AreEqual(start, legacy.FindLineStart(line));
            Assert.AreEqual(lines[line - 1], tree.GetLineContent(line));
            Assert.AreEqual(lines[line - 1], legacy.GetLineContent(line));
            start += lines[line - 1].Length;
            if (start < text.Length)
            {
                start += text[start] == '\r' && start + 1 < text.Length && text[start + 1] == '\n' ? 2 : 1;
            }
        }
        Assert.AreEqual(-1L, tree.FindLineStart(lines.Length + 1));
        Assert.IsNull(tree.GetLineContent(lines.Length + 1));
        Assert.IsNull(legacy.GetLineContent(lines.Length + 1));
        for (var i = 0; i <= text.Length; i++)
        {
            var summary = LineNumberDimension.Instance.SummarizeSpan(text.AsSpan(0, i));
            var expected = (summary.Lines + 1, summary.LastLineCharacters + 1);
            Assert.AreEqual(expected, tree.GetLineAndColumn(i));
            Assert.AreEqual(expected, legacy.GetLineAndColumn(i));
        }
        Assert.AreEqual("", default(TextTree).GetLineContent(1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.FindLineStart(0));
    }

    [TestMethod]
    public void RangeQueriesScanOnlyBoundaryLeavesAndReuseFullSummaries()
    {
        var tree = new SumTree<char, long, CountOperations>(new string('a', 2_097_152).AsMemory());
        CountOperations.Reset();
        Assert.AreEqual(tree.Length, tree.GetRangeSummary(0, tree.Length));
        Assert.AreEqual(0L, CountOperations.Scanned);
        Assert.AreEqual(0L, CountOperations.Combines);
        Assert.AreEqual(1_000_003L, tree.GetRangeSummary(17, 1_000_003));
        Assert.IsLessThanOrEqualTo(2048, CountOperations.Scanned);
        Assert.IsLessThanOrEqualTo(2 * tree.Depth * 8, CountOperations.Combines);
    }

    [TestMethod]
    public void TypedQueriesAndConcreteTraversalDoNotAllocate()
    {
        var tree = new TextTree(new string('x', 2_097_152).AsMemory());
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
        Assert.IsGreaterThan(0, result);
        Assert.AreEqual(0L, allocated);
    }
}
