using BalanceTree = SumTree.SumTree<char, SumTree.BracketBalanceSummary, SumTree.BracketBalanceSummaryOperations>;

namespace SumTree.Tests;
public class BracketBalanceTests
{
    [Test]
    [Arguments("", true, true)]
    [Arguments("({[]})", true, true)]
    [Arguments(")(", true, false)]
    [Arguments("([)]", true, true)]
    [Arguments("(()", false, false)]
    [Arguments("\"(\" // )", true, true)]
    public async Task RawCountsAndPrefixBalancesHaveDifferentContracts(string text, bool equalCounts, bool balancedPrefixes)
    {
        var counts = BracketCountSummary.SummarizeSpan(text);
        var summary = BracketBalanceSummaryOperations.SummarizeSpan(text);
        await Assert.That(counts.HasEqualCounts).IsEqualTo(equalCounts);
        await Assert.That(counts.IsBalanced).IsEqualTo(equalCounts);
        await Assert.That(summary.HasBalancedPrefixes).IsEqualTo(balancedPrefixes);
        await Assert.That(summary).IsEqualTo(Reference(text));
    }

    [Test]
    public async Task PrefixBalancesAreAssociativeAcrossEveryPartition()
    {
        var random = new Random(633);
        const string alphabet = "()[]{} x\r\n";
        for (var sample = 0; sample < 100; sample++)
        {
            var text = new string([.. Enumerable.Range(0, 25).Select(_ => alphabet[random.Next(alphabet.Length)])]);
            var expected = Reference(text);
            await Assert.That(BracketBalanceSummaryOperations.SummarizeSpan(text)).IsEqualTo(expected);
            for (var first = 0; first <= text.Length; first++)
            {
                for (var second = first; second <= text.Length; second++)
                {
                    var a = BracketBalanceSummaryOperations.SummarizeSpan(text.AsSpan(0, first));
                    var b = BracketBalanceSummaryOperations.SummarizeSpan(text.AsSpan(first, second - first));
                    var c = BracketBalanceSummaryOperations.SummarizeSpan(text.AsSpan(second));
                    await Assert.That((a + b) + c).IsEqualTo(expected);
                    await Assert.That(a + (b + c)).IsEqualTo(expected);
                }
            }
            var composed = BracketBalanceSummary.Identity;
            foreach (var element in text)
            {
                composed += BracketBalanceSummary.FromCharacter(element);
            }
            await Assert.That(composed).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task EditedSnapshotsRangesAndFirstInvalidPrefixMatchLinearScan()
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
            await Assert.That(tree.Summary).IsEqualTo(Reference(text));
            index = random.Next(text.Length + 1);
            var length = random.Next(text.Length - index + 1);
            await Assert.That(tree.GetRangeSummary(index, length)).IsEqualTo(Reference(text.Substring(index, length)));
            var depth = 0;
            long expected = -1;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '(') { depth++; }
                if (text[i] == ')') { depth--; }
                if (depth < 0) { expected = i; break; }
            }
            await Assert.That(tree.FindPosition(new NegativeParenthesisPrefix())).IsEqualTo(expected);
        }
        await Assert.That(snapshot.ToString()).IsEqualTo(original);
        await Assert.That(snapshot.Summary).IsEqualTo(Reference(original));
        tree.ValidateStructure();
    }

    [Test]
    public async Task SummaryCounterOverflowThrowsInsteadOfWrapping()
    {
        var line = new LineNumberSummary(0, int.MaxValue, int.MaxValue);
        await Assert.That(() => line + LineNumberSummary.FromCharacter('x')).ThrowsExactly<OverflowException>();
        var counts = new BracketCountSummary(int.MaxValue, 0, 0, 0, 0, 0);
        await Assert.That(() => counts + BracketCountSummary.FromCharacter('(')).ThrowsExactly<OverflowException>();
        var total = new BracketCountSummary(int.MaxValue, 0, 1, 0, 0, 0);
        await Assert.That(() => total.TotalOpenBrackets).ThrowsExactly<OverflowException>();
        var balance = new DelimiterBalanceSummary(long.MaxValue, 0);
        await Assert.That(() => balance + new DelimiterBalanceSummary(1, 0)).ThrowsExactly<OverflowException>();
        await Assert.That(() => new DelimiterBalanceSummary(long.MinValue, long.MinValue) + new DelimiterBalanceSummary(-1, -1)).ThrowsExactly<OverflowException>();
    }

    [Test]
    public async Task LargeSharedSnapshotsRejectIntCounterOverflowWithoutChangingEarlierVersions()
    {
        var tree = new SumTree<char, TextSummary, TextSummaryOperations>(new string('x', 1024).AsMemory());
        for (var i = 0; i < 20; i++) { tree += tree; }
        await Assert.That(tree.Length).IsEqualTo(1_073_741_824L);
        await Assert.That(() => tree + tree).ThrowsExactly<OverflowException>();
        await Assert.That(tree.Summary.Lines.TotalCharacters).IsEqualTo(1_073_741_824);

        var newlines = new SumTree<char, TextSummary, TextSummaryOperations>(new string('\n', 1024).AsMemory());
        for (var i = 0; i < 20; i++) { newlines += newlines; }
        newlines += newlines.RemoveAt(newlines.Length - 1);
        await Assert.That(newlines.Length).IsEqualTo(int.MaxValue);
        await Assert.That(() => newlines.GetLineAndColumn(newlines.Length)).ThrowsExactly<OverflowException>();
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