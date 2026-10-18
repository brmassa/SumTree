using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BalanceTree = SumTree.SumTree<char, SumTree.BracketBalanceSummary, SumTree.BracketBalanceSummaryOperations>;

namespace SumTree.Tests;

[TestClass]
public class BracketBalanceTests
{
    [TestMethod]
    [DataRow("", true, true)]
    [DataRow("({[]})", true, true)]
    [DataRow(")(", true, false)]
    [DataRow("([)]", true, true)]
    [DataRow("(()", false, false)]
    [DataRow("\"(\" // )", true, true)]
    public void RawCountsAndPrefixBalancesHaveDifferentContracts(string text, bool equalCounts, bool balancedPrefixes)
    {
        var counts = BracketCountDimension.Instance.SummarizeSpan(text);
        var summary = BracketBalanceSummaryOperations.SummarizeSpan(text);
        Assert.AreEqual(equalCounts, counts.HasEqualCounts);
        Assert.AreEqual(equalCounts, counts.IsBalanced);
        Assert.AreEqual(balancedPrefixes, summary.HasBalancedPrefixes);
        Assert.AreEqual(Reference(text), summary);
        var tree = text.ToSumTreeWithLinesAndBrackets();
        Assert.AreEqual(equalCounts, tree.HaveEqualBracketCounts(text.Length));
        Assert.AreEqual(equalCounts, tree.AreBracketsBalanced(text.Length));
    }

    [TestMethod]
    public void PrefixBalancesAreAssociativeAcrossEveryPartition()
    {
        var random = new Random(633);
        const string alphabet = "()[]{} x\r\n";
        for (var sample = 0; sample < 100; sample++)
        {
            var text = new string(Enumerable.Range(0, 25).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            var expected = Reference(text);
            Assert.AreEqual(expected, BracketBalanceSummaryOperations.SummarizeSpan(text));
            for (var first = 0; first <= text.Length; first++)
                for (var second = first; second <= text.Length; second++)
                {
                    var a = BracketBalanceSummaryOperations.SummarizeSpan(text.AsSpan(0, first));
                    var b = BracketBalanceSummaryOperations.SummarizeSpan(text.AsSpan(first, second - first));
                    var c = BracketBalanceSummaryOperations.SummarizeSpan(text.AsSpan(second));
                    Assert.AreEqual(expected, (a + b) + c);
                    Assert.AreEqual(expected, a + (b + c));
                }
            var composed = BracketBalanceSummary.Identity;
            foreach (var element in text) composed += BracketBalanceSummary.FromCharacter(element);
            Assert.AreEqual(expected, composed);
        }
    }

    [TestMethod]
    public void EditedSnapshotsRangesAndFirstInvalidPrefixMatchLinearScan()
    {
        var random = new Random(72);
        var text = new string('x', 1023) + ")(" + string.Concat(Enumerable.Repeat("([]{})", 1000));
        var tree = new BalanceTree(text.AsMemory());
        var snapshot = tree;
        var original = text;
        for (var edit = 0; edit < 200; edit++)
        {
            var index = random.Next(text.Length + 1);
            const string inserted = "({)}][";
            tree = tree.InsertRange(index, inserted.AsMemory());
            text = text.Insert(index, inserted);
            if (edit % 3 == 0)
            {
                index = random.Next(text.Length - 5);
                tree = tree.RemoveRange(index, 5);
                text = text.Remove(index, 5);
            }
            Assert.AreEqual(Reference(text), tree.Summary);
            index = random.Next(text.Length + 1);
            var length = random.Next(text.Length - index + 1);
            Assert.AreEqual(Reference(text.Substring(index, length)), tree.GetRangeSummary(index, length));
            var depth = 0;
            long expected = -1;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                if (text[i] == ')') depth--;
                if (depth < 0) { expected = i; break; }
            }
            Assert.AreEqual(expected, tree.FindPosition(new NegativeParenthesisPrefix()));
        }
        Assert.AreEqual(original, snapshot.ToString());
        Assert.AreEqual(Reference(original), snapshot.Summary);
        tree.ValidateStructure();
        var legacy = text.ToSumTree().WithDimension(BracketBalanceDimension.Instance);
        Assert.AreEqual(tree.Summary, legacy.GetSummary<BracketBalanceSummary>());
        Assert.AreEqual(tree.FindPosition(new NegativeParenthesisPrefix()),
            legacy.FindPosition<BracketBalanceSummary>(s => s.Parentheses.MinimumPrefixBalance < 0));
    }

    [TestMethod]
    public void SummaryCounterOverflowThrowsInsteadOfWrapping()
    {
        var line = new LineNumberSummary(0, int.MaxValue, int.MaxValue);
        Assert.ThrowsExactly<OverflowException>(() => line + LineNumberSummary.FromCharacter('x'));
        var counts = new BracketCountSummary(int.MaxValue, 0, 0, 0, 0, 0);
        Assert.ThrowsExactly<OverflowException>(() => counts + BracketCountSummary.FromCharacter('('));
        var total = new BracketCountSummary(int.MaxValue, 0, 1, 0, 0, 0);
        Assert.ThrowsExactly<OverflowException>(() => total.TotalOpenBrackets);
        var balance = new DelimiterBalanceSummary(long.MaxValue, 0);
        Assert.ThrowsExactly<OverflowException>(() => balance + new DelimiterBalanceSummary(1, 0));
        Assert.ThrowsExactly<OverflowException>(() => new DelimiterBalanceSummary(long.MinValue, long.MinValue) + new DelimiterBalanceSummary(-1, -1));
    }

    [TestMethod]
    public void LargeSharedSnapshotsRejectIntCounterOverflowWithoutChangingEarlierVersions()
    {
        var tree = new SumTree<char, TextSummary, TextSummaryOperations>(new string('x', 1024).AsMemory());
        for (var i = 0; i < 20; i++) tree += tree;
        Assert.AreEqual(1_073_741_824L, tree.Length);
        Assert.ThrowsExactly<OverflowException>(() => tree + tree);
        Assert.AreEqual(1_073_741_824, tree.Summary.Lines.TotalCharacters);

        var newlines = new SumTree<char, TextSummary, TextSummaryOperations>(new string('\n', 1024).AsMemory());
        for (var i = 0; i < 20; i++) newlines += newlines;
        newlines += newlines.RemoveAt(newlines.Length - 1);
        Assert.AreEqual((long)int.MaxValue, newlines.Length);
        Assert.ThrowsExactly<OverflowException>(() => newlines.GetLineAndColumn(newlines.Length));
    }

    [TestMethod]
    public void LineColumnInsertionRejectsColumnsOutsideTheSelectedLine()
    {
        var tree = "ab\r\nc\r".ToSumTreeWithLines();
        Assert.AreEqual("ab!\r\nc\r", tree.InsertAtLineColumn(1, 3, "!").ToString());
        Assert.AreEqual("ab\r\nc\r!", tree.InsertAtLineColumn(3, 1, "!").ToString());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.InsertAtLineColumn(1, 0, "!"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.InsertAtLineColumn(1, 4, "!"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.InsertAtLineColumn(3, 2, "!"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.InsertAtLineColumn(4, 1, "!"));
    }

    static BracketBalanceSummary Reference(string text)
    {
        DelimiterBalanceSummary Pair(char open, char close)
        {
            long balance = 0, minimum = 0;
            foreach (var element in text)
            {
                balance += element == open ? 1 : element == close ? -1 : 0;
                minimum = Math.Min(minimum, balance);
            }
            return new(balance, minimum);
        }
        return new(Pair('(', ')'), Pair('[', ']'), Pair('{', '}'));
    }

    readonly struct NegativeParenthesisPrefix : ISummarySeek<BracketBalanceSummary>
    {
        public bool IsReached(in BracketBalanceSummary summary) => summary.Parentheses.MinimumPrefixBalance < 0;
    }
}
