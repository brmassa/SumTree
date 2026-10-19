using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SumTree.TreeMap;

namespace SumTree.Tests;

[TestClass]
public class TreeSetTests
{
    [TestMethod]
    public void EmptyTreeSetShouldHaveZeroCount()
    {
        var set = new TreeSet<int>();

        Assert.IsTrue(set.IsEmpty);
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void AddSingleItemShouldReturnTrue()
    {
        var set = new TreeSet<int>();

        var added = set.Add(1);

        Assert.IsTrue(added);
        Assert.AreEqual(1, set.Count);
        Assert.IsFalse(set.IsEmpty);
        Assert.IsTrue(set.Contains(1));
    }

    [TestMethod]
    public void AddDuplicateItemShouldReturnFalse()
    {
        var set = new TreeSet<int> { 1 };

        var added = set.Add(1);

        Assert.IsFalse(added);
        Assert.AreEqual(1, set.Count);
    }

    [TestMethod]
    public void AddMultipleItemsShouldMaintainOrder()
    {
        var set = new TreeSet<int>
        {
            3,
            1,
            2
        };

        var items = set.ToList();
        Assert.AreEqual(3, items.Count);
        Assert.AreEqual(1, items[0]);
        Assert.AreEqual(2, items[1]);
        Assert.AreEqual(3, items[2]);
    }

    [TestMethod]
    public void RemoveExistingItemShouldReturnTrue()
    {
        var set = new TreeSet<int>
        {
            1,
            2
        };

        var removed = set.Remove(1);

        Assert.IsTrue(removed);
        Assert.IsFalse(set.Contains(1));
        Assert.AreEqual(1, set.Count);
    }

    [TestMethod]
    public void RemoveNonexistentItemShouldReturnFalse()
    {
        var set = new TreeSet<int>();

        var removed = set.Remove(999);

        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void ContainsExistingItemShouldReturnTrue()
    {
        var set = new TreeSet<int> { 1 };

        Assert.IsTrue(set.Contains(1));
    }

    [TestMethod]
    public void ContainsNonexistentItemShouldReturnFalse()
    {
        var set = new TreeSet<int>();

        Assert.IsFalse(set.Contains(999));
    }

    [TestMethod]
    public void ClearShouldRemoveAllItems()
    {
        var set = new TreeSet<int>
        {
            1,
            2
        };

        set.Clear();

        Assert.IsTrue(set.IsEmpty);
        Assert.AreEqual(0, set.Count);
    }

    [TestMethod]
    public void ExtendShouldAddMultipleItems()
    {
        var set = new TreeSet<int>();
        int[] items = [3, 1, 2, 1]; // Include duplicate

        set.Extend(items);

        Assert.AreEqual(3, set.Count); // Should not contain duplicates
        Assert.IsTrue(set.Contains(1));
        Assert.IsTrue(set.Contains(2));
        Assert.IsTrue(set.Contains(3));
    }

    [TestMethod]
    public void IterateFromShouldStartFromSpecifiedItem()
    {
        var set = new TreeSet<int>
        {
            1,
            2,
            3,
            4
        };

        var items = set.IterateFrom(2).ToList();

        Assert.AreEqual(3, items.Count);
        Assert.AreEqual(2, items[0]);
        Assert.AreEqual(3, items[1]);
        Assert.AreEqual(4, items[2]);
    }

    [TestMethod]
    public void FirstShouldReturnFirstItem()
    {
        var set = new TreeSet<int>
        {
            3,
            1,
            2
        };

        var first = set.First();

        Assert.AreEqual(1, first.Value);
    }

    [TestMethod]
    public void FirstEmptySetShouldReturnNull()
    {
        var set = new TreeSet<int>();

        var first = set.First();

        Assert.AreEqual(false, first.HasValue);
    }

    [TestMethod]
    public void LastShouldReturnLastItem()
    {
        var set = new TreeSet<int>
        {
            3,
            1,
            2
        };

        var last = set.Last();

        Assert.AreEqual(3, last.Value);
    }

    [TestMethod]
    public void LastEmptySetShouldReturnNull()
    {
        var set = new TreeSet<int>();

        var last = set.Last();

        Assert.AreEqual(false, last.HasValue);
    }

    [TestMethod]
    public void ClosestShouldReturnClosestItem()
    {
        var set = new TreeSet<int>
        {
            1,
            3,
            5
        };

        var closest = set.Closest(2);

        Assert.AreEqual(1, closest.Value);
    }

    [TestMethod]
    public void ClosestNoSmallerItemShouldReturnFalse()
    {
        var set = new TreeSet<int>
        {
            3,
            5
        };

        var closest = set.Closest(1);

        Assert.AreEqual(false, closest.HasValue);
    }

    [TestMethod]
    public void ExceptShouldReturnDifference()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        var set2 = new TreeSet<int>
        {
            2,
            3,
            4
        };

        var difference = set1.Except(set2);

        Assert.AreEqual(1, difference.Count);
        Assert.IsTrue(difference.Contains(1));
    }

    [TestMethod]
    public void IntersectShouldReturnIntersection()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        var set2 = new TreeSet<int>
        {
            2,
            3,
            4
        };

        var intersection = set1.Intersect(set2);

        Assert.AreEqual(2, intersection.Count);
        Assert.IsTrue(intersection.Contains(2));
        Assert.IsTrue(intersection.Contains(3));
    }

    [TestMethod]
    public void UnionShouldReturnUnion()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2
        };

        var set2 = new TreeSet<int>
        {
            2,
            3
        };

        var union = set1.Union(set2);

        Assert.AreEqual(3, union.Count);
        Assert.IsTrue(union.Contains(1));
        Assert.IsTrue(union.Contains(2));
        Assert.IsTrue(union.Contains(3));
    }

    [TestMethod]
    public void SymmetricExceptShouldReturnSymmetricDifference()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        var set2 = new TreeSet<int>
        {
            2,
            3,
            4
        };

        var symmetricDiff = set1.SymmetricExcept(set2);

        Assert.AreEqual(2, symmetricDiff.Count);
        Assert.IsTrue(symmetricDiff.Contains(1));
        Assert.IsTrue(symmetricDiff.Contains(4));
        Assert.IsFalse(symmetricDiff.Contains(2));
        Assert.IsFalse(symmetricDiff.Contains(3));
    }

    [TestMethod]
    public void IsSubsetOfTrueCaseShouldReturnTrue()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2
        };

        var set2 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        Assert.IsTrue(set1.IsSubsetOf(set2));
    }

    [TestMethod]
    public void IsSubsetOfFalseCaseShouldReturnFalse()
    {
        var set1 = new TreeSet<int>
        {
            1,
            4
        };

        var set2 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        Assert.IsFalse(set1.IsSubsetOf(set2));
    }

    [TestMethod]
    public void IsSupersetOfTrueCaseShouldReturnTrue()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        var set2 = new TreeSet<int>
        {
            1,
            2
        };

        Assert.IsTrue(set1.IsSupersetOf(set2));
    }

    [TestMethod]
    public void IsSupersetOfFalseCaseShouldReturnFalse()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2
        };

        var set2 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        Assert.IsFalse(set1.IsSupersetOf(set2));
    }

    [TestMethod]
    public void OverlapsTrueCaseShouldReturnTrue()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2
        };

        var set2 = new TreeSet<int>
        {
            2,
            3
        };

        Assert.IsTrue(set1.Overlaps(set2));
    }

    [TestMethod]
    public void OverlapsFalseCaseShouldReturnFalse()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2
        };

        var set2 = new TreeSet<int>
        {
            3,
            4
        };

        Assert.IsFalse(set1.Overlaps(set2));
    }

    [TestMethod]
    public void SetEqualsTrueCaseShouldReturnTrue()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        var set2 = new TreeSet<int>
        {
            3,
            1,
            2
        };

        Assert.IsTrue(set1.SetEquals(set2));
    }

    [TestMethod]
    public void SetEqualsFalseCaseShouldReturnFalse()
    {
        var set1 = new TreeSet<int>
        {
            1,
            2
        };

        var set2 = new TreeSet<int>
        {
            1,
            2,
            3
        };

        Assert.IsFalse(set1.SetEquals(set2));
    }

    [TestMethod]
    public void FromOrderedEntriesShouldCreateSetFromOrderedData()
    {
        int[] entries = [1, 2, 3, 2, 1]; // Include duplicates

        var set = new TreeSet<int>(entries);

        Assert.AreEqual(3, set.Count);
        Assert.IsTrue(set.Contains(1));
        Assert.IsTrue(set.Contains(2));
        Assert.IsTrue(set.Contains(3));
    }

    [TestMethod]
    public void EnumerationShouldIterateInOrder()
    {
        var set = new TreeSet<int>
        {
            3,
            1,
            2
        };

        var items = new List<int>();
        foreach (var item in set)
        {
            items.Add(item);
        }

        Assert.AreEqual(3, items.Count);
        Assert.AreEqual(1, items[0]);
        Assert.AreEqual(2, items[1]);
        Assert.AreEqual(3, items[2]);
    }

    [TestMethod]
    public void StringItemsShouldMaintainLexicographicOrder()
    {
        var set = new TreeSet<string>
        {
            "zebra",
            "apple",
            "banana"
        };

        var items = set.ToList();

        Assert.AreEqual(3, items.Count);
        Assert.AreEqual("apple", items[0]);
        Assert.AreEqual("banana", items[1]);
        Assert.AreEqual("zebra", items[2]);
    }

    //[TestMethod]
    public void LargeDataSetShouldMaintainPerformance()
    {
        var set = new TreeSet<int>();

        // Add 10,000 items
        for (var i = 0; i < 10000; i++)
        {
            set.Add(i);
        }

        Assert.AreEqual(10000, set.Count);

        // Verify some random accesses
        Assert.IsTrue(set.Contains(0));
        Assert.IsTrue(set.Contains(5000));
        Assert.IsTrue(set.Contains(9999));

        // Remove half the items
        for (var i = 0; i < 5000; i++)
        {
            set.Remove(i);
        }

        Assert.AreEqual(5000, set.Count);
        Assert.IsFalse(set.Contains(0));
        Assert.IsTrue(set.Contains(5000));
    }

    [TestMethod]
    public void EmptySetOperationsShouldHandleGracefully()
    {
        var emptySet = new TreeSet<int>();
        var otherSet = new TreeSet<int> { 1 };

        Assert.IsTrue(emptySet.Except(otherSet).IsEmpty);
        Assert.IsTrue(emptySet.Intersect(otherSet).IsEmpty);
        Assert.AreEqual(1, emptySet.Union(otherSet).Count);
        Assert.AreEqual(1, emptySet.SymmetricExcept(otherSet).Count);
        Assert.IsTrue(emptySet.IsSubsetOf(otherSet));
        Assert.IsFalse(emptySet.IsSupersetOf(otherSet));
        Assert.IsFalse(emptySet.Overlaps(otherSet));
        Assert.IsFalse(emptySet.SetEquals(otherSet));
    }

    [TestMethod]
    public void SetOperationsShouldMatchReferenceImplementationOnLargerSets()
    {
        var leftItems = new List<int>();
        var rightItems = new List<int>();
        for (var i = 0; i < 500; i++)
        {
            if (i % 3 == 0) { leftItems.Add(i); }
            if (i % 2 == 0) { rightItems.Add(i); }
        }

        var left = new TreeSet<int>(leftItems);
        var right = new TreeSet<int>(rightItems);

        CollectionAssert.AreEquivalent(leftItems.Except(rightItems).ToList(), left.Except(right).ToList());
        CollectionAssert.AreEquivalent(leftItems.Intersect(rightItems).ToList(), left.Intersect(right).ToList());
        CollectionAssert.AreEquivalent(leftItems.Union(rightItems).ToList(), left.Union(right).ToList());
        CollectionAssert.AreEquivalent(leftItems.Except(rightItems).Union(rightItems.Except(leftItems)).ToList(),
            left.SymmetricExcept(right).ToList());

        Assert.IsTrue(left.IsSubsetOf(left.Union(right)));
        Assert.IsFalse(left.IsSubsetOf(right));
        Assert.IsTrue(left.IsSupersetOf(left.Intersect(right)));
        Assert.IsTrue(left.Overlaps(right));
        Assert.IsFalse(left.SetEquals(right));
        Assert.IsTrue(left.SetEquals([.. leftItems]));
    }

    [TestMethod]
    public void ExtendOnLargeBatchShouldRebuildOnceAndKeepSortedOrder()
    {
        var set = new TreeSet<int> { 10 };
        var batch = new List<int>();
        for (var i = 1000; i >= 0; i--)
        {
            batch.Add(i);
        }

        set.Extend(batch);

        Assert.AreEqual(1001, set.Count);
        Assert.IsTrue(set.Contains(0));
        Assert.IsTrue(set.Contains(1000));
        var items = new List<int>();
        foreach (var item in set)
        {
            items.Add(item);
        }

        for (var i = 1; i < items.Count; i++)
        {
            Assert.IsTrue(items[i - 1] < items[i]);
        }
    }
}
