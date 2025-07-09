using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SumTree.Tests;

[TestClass]
public class EditTests
{
    [TestMethod]
    public void InsertEdit_Apply_ShouldInsertAtCorrectPosition()
    {
        var tree = "hello".ToSumTree();
        var edit = new InsertEdit<char>(2, ' ');

        var result = edit.Apply(tree);

        Assert.AreEqual("he llo", new string(result.ToArray()));
    }

    [TestMethod]
    public void InsertEdit_WithMultipleItems_ShouldInsertAll()
    {
        var tree = "hello".ToSumTree();
        var edit = new InsertEdit<char>(2, " world".AsMemory());

        var result = edit.Apply(tree);

        Assert.AreEqual("he worldllo", new string(result.ToArray()));
    }

    [TestMethod]
    public void InsertEdit_WithSumTree_ShouldInsertTree()
    {
        var tree = "hello".ToSumTree();
        var insertTree = " world".ToSumTree();
        var edit = new InsertEdit<char>(2, insertTree);

        var result = edit.Apply(tree);

        Assert.AreEqual("he worldllo", new string(result.ToArray()));
    }

    [TestMethod]
    public void InsertEdit_Position_ShouldReturnCorrectPosition()
    {
        var edit = new InsertEdit<char>(5, 'x');

        Assert.AreEqual(5, edit.Position);
        Assert.AreEqual(5, edit.Key);
    }

    [TestMethod]
    public void InsertEdit_LengthChange_ShouldReturnInsertedLength()
    {
        var edit = new InsertEdit<char>(0, "test".AsMemory());

        Assert.AreEqual(4, edit.LengthChange);
    }

    [TestMethod]
    public void RemoveEdit_Apply_ShouldRemoveAtCorrectPosition()
    {
        var tree = "hello world".ToSumTree();
        var edit = new RemoveEdit<char>(5, 6); // Remove " world"

        var result = edit.Apply(tree);

        Assert.AreEqual("hello", new string(result.ToArray()));
    }

    [TestMethod]
    public void RemoveEdit_Position_ShouldReturnCorrectPosition()
    {
        var edit = new RemoveEdit<char>(5, 3);

        Assert.AreEqual(5, edit.Position);
        Assert.AreEqual(5, edit.Key);
    }

    [TestMethod]
    public void RemoveEdit_LengthChange_ShouldReturnNegativeLength()
    {
        var edit = new RemoveEdit<char>(0, 5);

        Assert.AreEqual(-5, edit.LengthChange);
    }

    [TestMethod]
    public void RemoveEdit_ConflictsWith_OverlappingRemove_ShouldReturnTrue()
    {
        var edit1 = new RemoveEdit<char>(5, 3); // Remove positions 5-7
        var edit2 = new RemoveEdit<char>(7, 3); // Remove positions 7-9

        Assert.IsTrue(edit1.ConflictsWith(edit2));
        Assert.IsTrue(edit2.ConflictsWith(edit1));
    }

    [TestMethod]
    public void RemoveEdit_ConflictsWith_NonOverlappingRemove_ShouldReturnFalse()
    {
        var edit1 = new RemoveEdit<char>(5, 2); // Remove positions 5-6
        var edit2 = new RemoveEdit<char>(8, 2); // Remove positions 8-9

        Assert.IsFalse(edit1.ConflictsWith(edit2));
        Assert.IsFalse(edit2.ConflictsWith(edit1));
    }

    [TestMethod]
    public void RemoveEdit_ConflictsWith_InsertInRange_ShouldReturnTrue()
    {
        var removeEdit = new RemoveEdit<char>(5, 3); // Remove positions 5-7
        var insertEdit = new InsertEdit<char>(6, 'x'); // Insert at position 6

        Assert.IsTrue(removeEdit.ConflictsWith(insertEdit));
    }

    [TestMethod]
    public void RemoveEdit_ConflictsWith_InsertOutsideRange_ShouldReturnFalse()
    {
        var removeEdit = new RemoveEdit<char>(5, 3); // Remove positions 5-7
        var insertEdit = new InsertEdit<char>(10, 'x'); // Insert at position 10

        Assert.IsFalse(removeEdit.ConflictsWith(insertEdit));
    }

    [TestMethod]
    public void ReplaceEdit_Apply_ShouldReplaceCorrectly()
    {
        var tree = "hello world".ToSumTree();
        var edit = new ReplaceEdit<char>(6, 5, "earth".AsMemory()); // Replace "world" with "earth"

        var result = edit.Apply(tree);

        Assert.AreEqual("hello earth", new string(result.ToArray()));
    }

    [TestMethod]
    public void ReplaceEdit_WithSingleItem_ShouldReplaceWithSingleItem()
    {
        var tree = "hello".ToSumTree();
        var edit = new ReplaceEdit<char>(1, 1, 'a'); // Replace 'e' with 'a'

        var result = edit.Apply(tree);

        Assert.AreEqual("hallo", new string(result.ToArray()));
    }

    [TestMethod]
    public void ReplaceEdit_WithSumTree_ShouldReplaceWithTree()
    {
        var tree = "hello".ToSumTree();
        var replaceTree = "ey".ToSumTree();
        var edit = new ReplaceEdit<char>(1, 1, replaceTree); // Replace 'e' with "ey"

        var result = edit.Apply(tree);

        Assert.AreEqual("heyllo", new string(result.ToArray()));
    }

    [TestMethod]
    public void ReplaceEdit_LengthChange_ShouldReturnCorrectDifference()
    {
        var edit = new ReplaceEdit<char>(0, 5, "test".AsMemory()); // Remove 5, insert 4

        Assert.AreEqual(-1, edit.LengthChange);
    }

    [TestMethod]
    public void ReplaceEdit_ConflictsWith_OverlappingReplace_ShouldReturnTrue()
    {
        var edit1 = new ReplaceEdit<char>(5, 3, "abc".AsMemory()); // Replace positions 5-7
        var edit2 = new ReplaceEdit<char>(7, 3, "def".AsMemory()); // Replace positions 7-9

        Assert.IsTrue(edit1.ConflictsWith(edit2));
        Assert.IsTrue(edit2.ConflictsWith(edit1));
    }

    [TestMethod]
    public void ReplaceEdit_ConflictsWith_NonOverlappingReplace_ShouldReturnFalse()
    {
        var edit1 = new ReplaceEdit<char>(5, 2, "ab".AsMemory()); // Replace positions 5-6
        var edit2 = new ReplaceEdit<char>(8, 2, "cd".AsMemory()); // Replace positions 8-9

        Assert.IsFalse(edit1.ConflictsWith(edit2));
        Assert.IsFalse(edit2.ConflictsWith(edit1));
    }

    [TestMethod]
    public void ApplyEdits_MultipleEdits_ShouldApplyInCorrectOrder()
    {
        var tree = "hello world".ToSumTree();
        var edits = new List<Edit<char>>
        {
            new InsertEdit<char>(0, "Hi ".AsMemory()),
            new RemoveEdit<char>(5, 1), // Remove space
            new ReplaceEdit<char>(10, 5, "earth".AsMemory()) // Replace "world" with "earth"
        };

        var result = tree.ApplyEdits(edits);

        // Expected: "Hi " + "hello" + "earth" = "Hi helloearth"
        Assert.AreEqual("Hi helloearth", new string(result.ToArray()));
    }

    [TestMethod]
    public void ApplyEdits_ConflictingEdits_ShouldThrowException()
    {
        var tree = "hello".ToSumTree();
        var edits = new List<Edit<char>>
        {
            new RemoveEdit<char>(1, 2), // Remove positions 1-2
            new InsertEdit<char>(1, 'x') // Insert at position 1
        };

        Assert.ThrowsException<InvalidOperationException>(() => tree.ApplyEdits(edits));
    }

    [TestMethod]
    public void ApplyEditsWithAdjustment_ShouldAdjustPositions()
    {
        var tree = "hello".ToSumTree();
        var edits = new List<Edit<char>>
        {
            new InsertEdit<char>(0, "Hi ".AsMemory()), // Insert at beginning
            new InsertEdit<char>(2, "XX".AsMemory()) // Insert at position 2 (should be adjusted)
        };

        var result = tree.ApplyEditsWithAdjustment(edits);

        // First edit: "Hi hello"
        // Second edit should be adjusted to position 2 + 3 = 5: "Hi heXXllo"
        Assert.AreEqual("Hi heXXllo", new string(result.ToArray()));
    }

    [TestMethod]
    public void MergeEdits_AdjacentInserts_ShouldMerge()
    {
        var edits = new List<Edit<char>>
        {
            new InsertEdit<char>(0, "ab".AsMemory()),
            new InsertEdit<char>(2, "cd".AsMemory()) // Adjacent to first insert
        };

        var merged = EditOperations.MergeEdits(edits);

        Assert.AreEqual(1, merged.Count);
        Assert.IsInstanceOfType(merged[0], typeof(InsertEdit<char>));

        var insertEdit = (InsertEdit<char>)merged[0];
        Assert.AreEqual(0, insertEdit.Position);
        Assert.AreEqual("abcd", new string(insertEdit.Content.ToArray()));
    }

    [TestMethod]
    public void MergeEdits_AdjacentRemoves_ShouldMerge()
    {
        var edits = new List<Edit<char>>
        {
            new RemoveEdit<char>(5, 2), // Remove positions 5-6
            new RemoveEdit<char>(7, 3) // Remove positions 7-9 (adjacent)
        };

        var merged = EditOperations.MergeEdits(edits);

        Assert.AreEqual(1, merged.Count);
        Assert.IsInstanceOfType(merged[0], typeof(RemoveEdit<char>));

        var removeEdit = (RemoveEdit<char>)merged[0];
        Assert.AreEqual(5, removeEdit.Position);
        Assert.AreEqual(5, removeEdit.Length);
    }

    [TestMethod]
    public void MergeEdits_NonAdjacentEdits_ShouldNotMerge()
    {
        var edits = new List<Edit<char>>
        {
            new InsertEdit<char>(0, "ab".AsMemory()),
            new InsertEdit<char>(5, "cd".AsMemory()) // Not adjacent
        };

        var merged = EditOperations.MergeEdits(edits);

        Assert.AreEqual(2, merged.Count);
    }

    [TestMethod]
    public void Edit_ToString_ShouldReturnReadableString()
    {
        var insertEdit = new InsertEdit<char>(5, "test".AsMemory());
        var removeEdit = new RemoveEdit<char>(10, 3);
        var replaceEdit = new ReplaceEdit<char>(15, 2, "xy".AsMemory());

        Assert.AreEqual("Insert at 5: 4 items", insertEdit.ToString());
        Assert.AreEqual("Remove at 10: 3 items", removeEdit.ToString());
        Assert.AreEqual("Replace at 15: remove 2, insert 2 items", replaceEdit.ToString());
    }

    [TestMethod]
    public void Edit_SamePosition_ShouldConflict()
    {
        var edit1 = new InsertEdit<char>(5, 'a');
        var edit2 = new InsertEdit<char>(5, 'b');

        Assert.IsTrue(edit1.ConflictsWith(edit2));
        Assert.IsTrue(edit2.ConflictsWith(edit1));
    }

    [TestMethod]
    public void Edit_DifferentPositions_ShouldNotConflict()
    {
        var edit1 = new InsertEdit<char>(5, 'a');
        var edit2 = new InsertEdit<char>(10, 'b');

        Assert.IsFalse(edit1.ConflictsWith(edit2));
        Assert.IsFalse(edit2.ConflictsWith(edit1));
    }

    [TestMethod]
    public void ApplyEdits_EmptyEditList_ShouldReturnOriginalTree()
    {
        var tree = "hello".ToSumTree();
        var edits = new List<Edit<char>>();

        var result = tree.ApplyEdits(edits);

        Assert.AreEqual(tree, result);
    }

    [TestMethod]
    public void ApplyEdits_ComplexScenario_ShouldApplyCorrectly()
    {
        var tree = "The quick brown fox jumps over the lazy dog".ToSumTree();
        var edits = new List<Edit<char>>
        {
            new ReplaceEdit<char>(0, 3, "A".AsMemory()), // "The" -> "A"
            new InsertEdit<char>(10, " very".AsMemory()), // Insert " very" after "brown"
            new RemoveEdit<char>(35, 4), // Remove "lazy"
            new ReplaceEdit<char>(40, 3, "cat".AsMemory()) // "dog" -> "cat"
        };

        var result = tree.ApplyEdits(edits);

        Assert.AreEqual("A quick brown very fox jumps over the  cat", new string(result.ToArray()));
    }

    [TestMethod]
    public void Edit_LargeData_ShouldMaintainPerformance()
    {
        var largeText = string.Concat(Enumerable.Range(0, 100000).Select(i => (char)('a' + (i % 26))));
        var tree = largeText.ToSumTree();

        var edits = new List<Edit<char>>
        {
            new InsertEdit<char>(0, "PREFIX".AsMemory()),
            new InsertEdit<char>(50000, "MIDDLE".AsMemory()),
            new InsertEdit<char>(99999, "SUFFIX".AsMemory())
        };

        var result = tree.ApplyEditsWithAdjustment(edits);

        Assert.AreEqual(largeText.Length + 18, result.Length); // 6 + 6 + 6 = 18 additional chars
        Assert.IsTrue(new string(result.ToArray()).StartsWith("PREFIX"));
        Assert.IsTrue(new string(result.ToArray()).EndsWith("SUFFIX"));
    }
}
