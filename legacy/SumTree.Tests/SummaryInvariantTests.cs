using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SumTree.Tests;

[TestClass]
public class SummaryInvariantTests
{
    [TestMethod]
    public void InstallingDimensionCachesDescendantsAndPreservesOriginal()
    {
        var original = new string('a', 100_000).ToSumTree();
        var dimension = new CountingDimension();
        var tree = original.WithDimension(dimension);

        Assert.IsFalse(original.HasDimension<int>());
        Assert.AreEqual(100_000, tree.GetSummary<int>());
        dimension.Scanned = 0;
        Assert.AreEqual(49_999L, tree.FindPosition<int>(count => count >= 50_000));
        Assert.IsTrue(dimension.Scanned <= 1024, $"Seek scanned {dimension.Scanned} characters");
    }

    [TestMethod]
    public void JoiningTreesInstallsMissingDimensionsInDescendants()
    {
        var dimension = new CountingDimension();
        var left = new string('a', 50_000).ToSumTree();
        var right = new string('b', 50_000).ToSumTree().WithDimension(dimension);
        var tree = left + right;

        Assert.AreEqual(100_000, tree.GetSummary<int>());
        dimension.Scanned = 0;
        Assert.AreEqual(24_999L, tree.FindPosition<int>(count => count >= 25_000));
        Assert.IsTrue(dimension.Scanned <= 1024, $"Seek scanned {dimension.Scanned} characters");
        Assert.IsFalse(left.HasDimension<int>());
    }

    [TestMethod]
    public void ReplacingDimensionRecomputesCachesWithoutChangingOldSnapshot()
    {
        var original = new string('a', 10_000).ToSumTree().WithDimension(new CountingDimension());
        var replacement = new CountingDimension(2);
        var tree = original.WithDimension(replacement);

        Assert.AreEqual(10_000, original.GetSummary<int>());
        Assert.AreEqual(20_000, tree.GetSummary<int>());
        replacement.Scanned = 0;
        Assert.AreEqual(4999L, tree.FindPosition<int>(count => count >= 10_000));
        Assert.IsTrue(replacement.Scanned <= 1024);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\r")]
    [DataRow("\n")]
    [DataRow("\r\n")]
    [DataRow("a\r\nb\rc\nd\r\n")]
    [DataRow("\n\r\r\n\n\r")]
    public void LineSummaryIsIndependentOfPartitionAndGrouping(string text)
    {
        var dimension = LineNumberDimension.Instance;
        var expected = dimension.SummarizeSpan(text);
        var byElement = dimension.Identity;
        foreach (var character in text)
        {
            byElement = dimension.Combine(byElement, dimension.SummarizeElement(character));
        }

        Assert.AreEqual(expected, byElement);
        for (var first = 0; first <= text.Length; first++)
        {
            for (var second = first; second <= text.Length; second++)
            {
                var a = dimension.SummarizeSpan(text.AsSpan(0, first));
                var b = dimension.SummarizeSpan(text.AsSpan(first, second - first));
                var c = dimension.SummarizeSpan(text.AsSpan(second));
                Assert.AreEqual(expected, dimension.Combine(dimension.Combine(a, b), c));
                Assert.AreEqual(expected, dimension.Combine(a, dimension.Combine(b, c)));
            }
        }
    }

    [TestMethod]
    public void CrLfAcrossLeafBoundarySurvivesSplitsAndEdits()
    {
        var source = new StringBuilder(new string('a', 1023) + "\r\nb\rc\n" + new string('d', 2000));
        var tree = source.ToString().ToSumTreeWithLines();
        var original = tree;
        var expected = LineNumberDimension.Instance.SummarizeSpan(source.ToString());
        Assert.AreEqual(3, expected.Lines);
        Assert.AreEqual(expected, tree.GetSummary<LineNumberSummary>());

        var (left, right) = tree.SplitAt(1024);
        Assert.AreEqual(expected, (left + right).GetSummary<LineNumberSummary>());
        Assert.AreEqual(1023L, tree.FindPosition<LineNumberSummary>(s => s.Lines >= 1));
        Assert.AreEqual(1026L, tree.FindPosition<LineNumberSummary>(s => s.Lines >= 2));

        tree = tree.InsertRange(1024, "x".AsMemory());
        source.Insert(1024, 'x');
        Assert.AreEqual(4, tree.GetSummary<LineNumberSummary>().Lines);
        Assert.AreEqual(LineNumberDimension.Instance.SummarizeSpan(source.ToString()),
            tree.GetSummary<LineNumberSummary>());
        Assert.AreEqual(expected, original.GetSummary<LineNumberSummary>());

        tree = tree.RemoveAt(1024);
        Assert.AreEqual(expected, tree.GetSummary<LineNumberSummary>());
        Assert.AreEqual(original.ToString(), tree.ToString());
    }

    sealed class CountingDimension(int weight = 1) : SummaryDimensionBase<char, int>
    {
        public int Scanned { get; set; }
        public override int Identity => 0;
        public override int SummarizeElement(char element)
        {
            Scanned++;
            return weight;
        }

        public override int Combine(int left, int right) => left + right;
    }
}
