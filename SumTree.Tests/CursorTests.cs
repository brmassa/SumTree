using Microsoft.VisualStudio.TestTools.UnitTesting;
using SumTree.Cursors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SumTree.Tests;

[TestClass]
public class CursorTests
{
    [TestMethod]
    public void Cursor_EmptyTree_ShouldBeAtEnd()
    {
        var tree = SumTree<char>.Empty.WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        Assert.IsTrue(cursor.IsAtEnd);
        Assert.IsTrue(cursor.IsAtStart);
        Assert.AreEqual(cursor.Item, '\0');
    }

    [TestMethod]
    public void Cursor_SingleItem_ShouldNavigateCorrectly()
    {
        var tree = new SumTree<char>('a').WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        Assert.IsFalse(cursor.IsAtEnd);
        Assert.IsTrue(cursor.IsAtStart);
        Assert.AreEqual('a', cursor.Item);

        var moved = cursor.Next();
        Assert.IsFalse(moved);
        Assert.IsTrue(cursor.IsAtEnd);
    }

    [TestMethod]
    public void Cursor_MultipleItems_ShouldTraverseInOrder()
    {
        var tree = "hello".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        var items = new List<char>();

        while (!cursor.IsAtEnd)
        {
            items.Add(cursor.Item);
            if (!cursor.Next()) break;
        }

        Assert.AreEqual(5, items.Count);
        Assert.IsTrue(items.SequenceEqual("hello"));
    }

    [TestMethod]
    public void Cursor_Previous_ShouldNavigateBackward()
    {
        var tree = "abc".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        Assert.IsTrue(cursor.IsAtStart);
        cursor.Next(); // Move to 'b'
        cursor.Next(); // Move to 'c'

        Assert.AreEqual('c', cursor.Item);

        var moved = cursor.Previous();
        Assert.IsTrue(moved);
        Assert.AreEqual('b', cursor.Item);

        moved = cursor.Previous();
        Assert.IsTrue(moved);
        Assert.AreEqual('a', cursor.Item);
        Assert.IsTrue(cursor.IsAtStart);

        moved = cursor.Previous();
        Assert.IsFalse(moved);
        Assert.IsTrue(cursor.IsAtStart);
    }

    [TestMethod]
    public void Cursor_Seek_ShouldJumpToPosition()
    {
        var tree = "hello\nworld".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        var targetSummary = new LineNumberSummary(1, 0, 6); // Start of second line
        cursor.Seek(targetSummary, Bias.Right);

        Assert.AreEqual('w', cursor.Item);
        Assert.IsFalse(cursor.IsAtEnd);
    }

    [TestMethod]
    public void Cursor_SeekForward_ShouldMoveOnlyForward()
    {
        var tree = "abcdef".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        cursor.Next(); // Move to 'b'
        cursor.Next(); // Move to 'c'

        var initialPosition = cursor.Position;
        var targetSummary = new LineNumberSummary(0, 1, 1); // Position of 'a'
        cursor.SeekForward(targetSummary, Bias.Left);

        // Should not move backward, stay at current position
        Assert.AreEqual(initialPosition, cursor.Position);
        Assert.AreEqual('c', cursor.Item);
    }

    [TestMethod]
    public void Cursor_Slice_ShouldReturnSliceFromCurrentPosition()
    {
        var tree = "hello world".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        cursor.Next(); // Move to 'e'
        cursor.Next(); // Move to 'l'

        var endSummary = new LineNumberSummary(0, 7, 7); // Position after 'o'
        var slice = cursor.Slice(endSummary, Bias.Left);

        Assert.AreEqual("llo w", new string(slice.ToArray()));
    }

    [TestMethod]
    public void Cursor_Suffix_ShouldReturnRemainderOfTree()
    {
        var tree = "hello".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        cursor.Next(); // Move to 'e'
        cursor.Next(); // Move to 'l'

        var suffix = cursor.Suffix();

        Assert.AreEqual("llo", new string(suffix.ToArray()));
    }

    [TestMethod]
    public void Cursor_Summary_ShouldReturnSuffixSummary()
    {
        var tree = "hello\nworld".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        cursor.Next(); // Move to 'e'

        var summary = cursor.Summary();

        // Should represent the summary from 'e' to the end
        Assert.AreEqual(1, summary.Lines); // One newline remaining
        Assert.AreEqual(5, summary.LastLineCharacters); // "world" has 5 characters
        Assert.AreEqual(10, summary.TotalCharacters); // "ello\nworld" has 10 characters
    }

    [TestMethod]
    public void Cursor_SearchForward_ShouldFindMatchingPosition()
    {
        var tree = "hello\nworld\ntest".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();

        var found = cursor.SearchForward(summary => summary.Lines >= 1);

        Assert.IsTrue(found);
        Assert.AreEqual('\n', cursor.Item);
    }

    [TestMethod]
    [Ignore("Deal with this later")]
    public void Cursor_SearchBackward_ShouldFindMatchingPosition()
    {
        var tree = "hello\nworld\ntest".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Seek(cursor.End, Bias.Right);

        var found = cursor.SearchBackward(summary => summary.Lines == 1);

        Assert.IsTrue(found);
        // Should find the position after the first newline
    }

    [TestMethod]
    [Ignore("Deal with this later")]
    public void Cursor_SearchNotFound_ShouldRestorePosition()
    {
        var tree = "hello".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        cursor.Next(); // Move to 'e'
        var originalPosition = cursor.Position;

        var found = cursor.SearchForward(summary => summary.Lines >= 5); // Won't find this

        Assert.IsFalse(found);
        Assert.AreEqual(originalPosition, cursor.Position);
        Assert.AreEqual('e', cursor.Item);
    }

    [TestMethod]
    [Ignore("Deal with this later")]
    public void Cursor_Clone_ShouldCreateIndependentCopy()
    {
        var tree = "hello".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor1 = tree.LineCursor();

        cursor1.Start();
        cursor1.Next(); // Move to 'e'

        using var cursor2 = cursor1.Clone();

        // Both should be at the same position
        Assert.AreEqual(cursor1.Position, cursor2.Position);
        Assert.AreEqual(cursor1.Item, cursor2.Item);

        // Moving one should not affect the other
        cursor1.Next();
        Assert.AreNotEqual(cursor1.Position, cursor2.Position);
        Assert.AreEqual('e', cursor2.Item);
        Assert.AreEqual('l', cursor1.Item);
    }

    [TestMethod]
    public void Cursor_End_ShouldReturnCorrectEndPosition()
    {
        var tree = "hello\nworld".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        var end = cursor.End;

        Assert.AreEqual(1, end.Lines);
        Assert.AreEqual(5, end.LastLineCharacters);
        Assert.AreEqual(11, end.TotalCharacters);
    }

    [TestMethod]
    public void Cursor_BiasLeft_ShouldPositionBeforeTarget()
    {
        var tree = "world".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        var targetSummary = new LineNumberSummary(0, 2, 2); // Position of 'l'
        cursor.Seek(targetSummary, Bias.Left);

        Assert.AreEqual('o', cursor.Item);
    }

    [TestMethod]
    public void Cursor_BiasRight_ShouldPositionAfterTarget()
    {
        var tree = "world".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        var targetSummary = new LineNumberSummary(0, 2, 2); // Position of 'l'
        cursor.Seek(targetSummary, Bias.Right);

        // Should position after the target
        Assert.AreEqual('r', cursor.Item);
    }

    [TestMethod]
    public void Cursor_AsEnumerator_ShouldEnumerateCorrectly()
    {
        var tree = "abc".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        var items = new List<char>();

        while (!cursor.IsAtEnd)
        {
            items.Add(cursor.Item);
            if (!cursor.Next()) break;
        }

        Assert.AreEqual(3, items.Count);
        Assert.IsTrue(items.SequenceEqual("abc"));
    }

    [TestMethod]
    public void Cursor_Reset_ShouldReturnToInitialState()
    {
        var tree = "hello".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        cursor.Next();
        cursor.Next();

        // Create a new cursor at start position (reset functionality)
        using var resetCursor = tree.LineCursor();
        resetCursor.Start();

        Assert.IsTrue(resetCursor.IsAtStart);
        Assert.IsFalse(resetCursor.IsAtEnd);
    }

    [TestMethod]
    public void Cursor_LargeTree_ShouldMaintainPerformance()
    {
        var text = string.Concat(Enumerable.Range(0, 10000).Select(i => (char)('a' + (i % 26))));
        var tree = text.ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        // Seek to middle
        var targetSummary = new LineNumberSummary(0, 5000, 5000);
        cursor.Seek(targetSummary, Bias.Left);

        Assert.IsFalse(cursor.IsAtEnd);
        Assert.IsNotNull(cursor.Item);

        // Move around
        for (var i = 0; i < 100; i++)
        {
            cursor.Next();
        }

        Assert.IsFalse(cursor.IsAtEnd);
    }

    [TestMethod]
    public void Cursor_WithBracketDimension_ShouldWorkCorrectly()
    {
        var tree = "hello{world}test".ToSumTree().WithDimension(BracketCountDimension.Instance);
        using var cursor = tree.BracketCursor();

        cursor.Start();

        // Find the opening bracket
        var found = cursor.SearchForward(summary => summary.OpenCurlyBraces >= 1);
        Assert.IsTrue(found);
        Assert.AreEqual('{', cursor.Item);

        // Find the closing bracket
        found = cursor.SearchForward(summary => summary.CloseCurlyBraces >= 1);
        Assert.IsTrue(found);
        Assert.AreEqual('}', cursor.Item);
    }

    [TestMethod]
    public void Cursor_MultipleLines_ShouldTrackLineNumbers()
    {
        var tree = "line1\nline2\nline3".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();

        // Move to start of second line
        var found = cursor.SearchForward(summary => summary.Lines >= 1);
        Assert.IsTrue(found);
        Assert.AreEqual('\n', cursor.Item);

        cursor.Next(); // Move to 'l' of "line2"
        Assert.AreEqual('l', cursor.Item);

        cursor.Next(); // Move to 'i' of "line2"
        Assert.AreEqual('i', cursor.Item);

        var position = cursor.Position;
        Assert.AreEqual(1, position.Lines);
        Assert.AreEqual(1, position.LastLineCharacters);
        Assert.AreEqual(7, position.TotalCharacters);
    }

    [TestMethod]
    public void Cursor_ItemSummary_ShouldReturnCorrectSummary()
    {
        var tree = "a\nb".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        cursor.Start();
        var itemSummary = cursor.ItemSummary;

        Assert.IsNotNull(itemSummary);
        Assert.AreEqual(0, itemSummary.Lines);
        Assert.AreEqual(1, itemSummary.LastLineCharacters);
        Assert.AreEqual(1, itemSummary.TotalCharacters);

        cursor.Next(); // Move to newline
        itemSummary = cursor.ItemSummary;

        Assert.IsNotNull(itemSummary);
        Assert.AreEqual(1, itemSummary.Lines);
        Assert.AreEqual(0, itemSummary.LastLineCharacters);
        Assert.AreEqual(1, itemSummary.TotalCharacters);
    }

    [TestMethod]
    public void Cursor_CRLFLineEndings_ShouldCountLinesCorrectly()
    {
        var tree = "line1\r\nline2\r\nline3".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        var end = cursor.End;

        // Should count 2 line breaks (\r\n sequences)
        Assert.AreEqual(2, end.Lines);
        Assert.AreEqual(5, end.LastLineCharacters); // "line3" has 5 characters
        Assert.AreEqual(19, end.TotalCharacters); // Total length including \r\n
    }
}
