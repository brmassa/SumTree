using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SumTree.Tests;

/// <summary>Text-editor workloads: many small edits, line lookups and summaries that stay exact.</summary>
[TestClass]
public class EditingPerformanceTests
{
    static string Lines(int count) =>
        string.Join("\n", Enumerable.Range(0, count).Select(i => $"line {i} with some text"));

    [TestMethod]
    public void FindPositionMatchesALinearScanOnEditedTrees()
    {
        var text = new StringBuilder(Lines(200));
        var tree = text.ToString().ToSumTreeWithLines();
        var random = new Random(7);
        for (var edit = 0; edit < 300; edit++)
        {
            var at = random.Next(text.Length + 1);
            var insert = random.Next(4) == 0 ? "\n" : ((char)('a' + random.Next(26))).ToString();
            text.Insert(at, insert);
            tree = tree.InsertRange(at, insert.AsMemory());
        }

        var source = text.ToString();
        for (var line = 1; line < 40; line++)
        {
            var target = line;
            var expected = IndexOfNth(source, '\n', line);
            Assert.AreEqual(expected, tree.FindPosition<LineNumberSummary>(summary => summary.Lines >= target));
        }

        Assert.AreEqual(-1, tree.FindPosition<LineNumberSummary>(summary => summary.Lines > source.Count(c => c == '\n')));
        Assert.AreEqual(source, tree.ToString());
    }

    [TestMethod]
    public void TypingOneCharacterAtATimeKeepsLeavesLarge()
    {
        var tree = Lines(50).ToSumTreeWithLines();
        var caret = 100L;
        for (var i = 0; i < 2000; i++)
        {
            tree = tree.InsertRange(caret, "x".AsMemory());
            caret++;
        }

        Assert.IsTrue(tree.BufferCount < 20, $"{tree.BufferCount} leaves after typing");
        Assert.AreEqual(Lines(50).Insert(100, new string('x', 2000)), tree.ToString());
    }

    [TestMethod]
    public void AppendingElementsWithoutDimensionsKeepsTheLineSummaryExact()
    {
        var tree = "a\nb".ToSumTreeWithLines();

        tree = tree.Add('\n').Add('c').Add('\n');

        Assert.AreEqual(3, tree.GetSummary<LineNumberSummary>().Lines);
        Assert.AreEqual(5, tree.FindPosition<LineNumberSummary>(summary => summary.Lines >= 3));
    }

    [TestMethod]
    public void LineLookupsOnALargeDocumentStayLogarithmic()
    {
        var tree = Lines(200_000).ToSumTreeWithLines();
        for (var i = 0; i < 50; i++)
        {
            tree = tree.InsertRange(tree.Length / 2 + i * 37, "edit\n".AsMemory());
        }

        var watch = Stopwatch.StartNew();
        var found = 0L;
        for (var line = 1; line < 200_000; line += 199)
        {
            var target = line;
            found += tree.FindPosition<LineNumberSummary>(summary => summary.Lines >= target);
        }

        watch.Stop();
        Assert.IsTrue(found > 0);
        Assert.IsTrue(watch.ElapsedMilliseconds < 500, $"1000 lookups took {watch.ElapsedMilliseconds} ms");
    }

    static long IndexOfNth(string text, char value, int occurrence)
    {
        var index = -1;
        for (var i = 0; i < occurrence; i++)
        {
            index = text.IndexOf(value, index + 1);
            if (index < 0)
            {
                return -1;
            }
        }

        return index;
    }
}
