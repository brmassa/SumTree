
namespace SumTree.Tests;
public class TreeSetTests
{
    [Test]
    public async Task EmptyTreeSetShouldHaveZeroCount()
    {
        var set = new TreeSet<int>();

        await Assert.That(set.IsEmpty).IsTrue();
        await Assert.That(set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddSingleItemShouldReturnTrue()
    {
        var set = new TreeSet<int>();

        var added = set.Add(1);

        await Assert.That(added).IsTrue();
        await Assert.That(set.Count).IsEqualTo(1);
        await Assert.That(set.IsEmpty).IsFalse();
        await Assert.That(set.Contains(1)).IsTrue();
    }

    [Test]
    public async Task AddDuplicateItemShouldReturnFalse()
    {
        var set = new TreeSet<int> { 1 };

        var added = set.Add(1);

        await Assert.That(added).IsFalse();
        await Assert.That(set.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddMultipleItemsShouldMaintainOrder()
    {
        var set = new TreeSet<int>
        {
            3,
            1,
            2
        };

        var items = set.ToList();
        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0]).IsEqualTo(1);
        await Assert.That(items[1]).IsEqualTo(2);
        await Assert.That(items[2]).IsEqualTo(3);
    }

    [Test]
    public async Task RemoveExistingItemShouldReturnTrue()
    {
        var set = new TreeSet<int>
        {
            1,
            2
        };

        var removed = set.Remove(1);

        await Assert.That(removed).IsTrue();
        await Assert.That(set.Contains(1)).IsFalse();
        await Assert.That(set.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveNonexistentItemShouldReturnFalse()
    {
        var set = new TreeSet<int>();

        var removed = set.Remove(999);

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task ContainsExistingItemShouldReturnTrue()
    {
        var set = new TreeSet<int> { 1 };

        await Assert.That(set.Contains(1)).IsTrue();
    }

    [Test]
    public async Task ContainsNonexistentItemShouldReturnFalse()
    {
        var set = new TreeSet<int>();

        await Assert.That(set.Contains(999)).IsFalse();
    }

    [Test]
    public async Task ClearShouldRemoveAllItems()
    {
        var set = new TreeSet<int>
        {
            1,
            2
        };

        set.Clear();

        await Assert.That(set.IsEmpty).IsTrue();
        await Assert.That(set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExtendShouldAddMultipleItems()
    {
        var set = new TreeSet<int>();
        int[] items = [3, 1, 2, 1]; // Include duplicate

        set.Extend(items);

        await Assert.That(set.Count).IsEqualTo(3); // Should not contain duplicates
        await Assert.That(set.Contains(1)).IsTrue();
        await Assert.That(set.Contains(2)).IsTrue();
        await Assert.That(set.Contains(3)).IsTrue();
    }

    [Test]
    public async Task IterateFromShouldStartFromSpecifiedItem()
    {
        var set = new TreeSet<int>
        {
            1,
            2,
            3,
            4
        };

        var items = set.IterateFrom(2).ToList();

        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0]).IsEqualTo(2);
        await Assert.That(items[1]).IsEqualTo(3);
        await Assert.That(items[2]).IsEqualTo(4);
    }

    [Test]
    public async Task FirstShouldReturnFirstItem()
    {
        var set = new TreeSet<int>
        {
            3,
            1,
            2
        };

        var first = set.First();

        await Assert.That(first.Value).IsEqualTo(1);
    }

    [Test]
    public async Task FirstEmptySetShouldReturnNull()
    {
        var set = new TreeSet<int>();

        var first = set.First();

        await Assert.That(first.HasValue).IsFalse();
    }

    [Test]
    public async Task LastShouldReturnLastItem()
    {
        var set = new TreeSet<int>
        {
            3,
            1,
            2
        };

        var last = set.Last();

        await Assert.That(last.Value).IsEqualTo(3);
    }

    [Test]
    public async Task LastEmptySetShouldReturnNull()
    {
        var set = new TreeSet<int>();

        var last = set.Last();

        await Assert.That(last.HasValue).IsFalse();
    }

    [Test]
    public async Task ClosestShouldReturnClosestItem()
    {
        var set = new TreeSet<int>
        {
            1,
            3,
            5
        };

        var closest = set.Closest(2);

        await Assert.That(closest.Value).IsEqualTo(1);
    }

    [Test]
    public async Task ClosestNoSmallerItemShouldReturnFalse()
    {
        var set = new TreeSet<int>
        {
            3,
            5
        };

        var closest = set.Closest(1);

        await Assert.That(closest.HasValue).IsFalse();
    }

    [Test]
    public async Task ExceptShouldReturnDifference()
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

        await Assert.That(difference.Count).IsEqualTo(1);
        await Assert.That(difference.Contains(1)).IsTrue();
    }

    [Test]
    public async Task IntersectShouldReturnIntersection()
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

        await Assert.That(intersection.Count).IsEqualTo(2);
        await Assert.That(intersection.Contains(2)).IsTrue();
        await Assert.That(intersection.Contains(3)).IsTrue();
    }

    [Test]
    public async Task UnionShouldReturnUnion()
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

        await Assert.That(union.Count).IsEqualTo(3);
        await Assert.That(union.Contains(1)).IsTrue();
        await Assert.That(union.Contains(2)).IsTrue();
        await Assert.That(union.Contains(3)).IsTrue();
    }

    [Test]
    public async Task SymmetricExceptShouldReturnSymmetricDifference()
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

        await Assert.That(symmetricDiff.Count).IsEqualTo(2);
        await Assert.That(symmetricDiff.Contains(1)).IsTrue();
        await Assert.That(symmetricDiff.Contains(4)).IsTrue();
        await Assert.That(symmetricDiff.Contains(2)).IsFalse();
        await Assert.That(symmetricDiff.Contains(3)).IsFalse();
    }

    [Test]
    public async Task IsSubsetOfTrueCaseShouldReturnTrue()
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

        await Assert.That(set1.IsSubsetOf(set2)).IsTrue();
    }

    [Test]
    public async Task IsSubsetOfFalseCaseShouldReturnFalse()
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

        await Assert.That(set1.IsSubsetOf(set2)).IsFalse();
    }

    [Test]
    public async Task IsSupersetOfTrueCaseShouldReturnTrue()
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

        await Assert.That(set1.IsSupersetOf(set2)).IsTrue();
    }

    [Test]
    public async Task IsSupersetOfFalseCaseShouldReturnFalse()
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

        await Assert.That(set1.IsSupersetOf(set2)).IsFalse();
    }

    [Test]
    public async Task OverlapsTrueCaseShouldReturnTrue()
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

        await Assert.That(set1.Overlaps(set2)).IsTrue();
    }

    [Test]
    public async Task OverlapsFalseCaseShouldReturnFalse()
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

        await Assert.That(set1.Overlaps(set2)).IsFalse();
    }

    [Test]
    public async Task SetEqualsTrueCaseShouldReturnTrue()
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

        await Assert.That(set1.SetEquals(set2)).IsTrue();
    }

    [Test]
    public async Task SetEqualsFalseCaseShouldReturnFalse()
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

        await Assert.That(set1.SetEquals(set2)).IsFalse();
    }

    [Test]
    public async Task FromOrderedEntriesShouldCreateSetFromOrderedData()
    {
        int[] entries = [1, 2, 3, 2, 1]; // Include duplicates

        var set = new TreeSet<int>(entries);

        await Assert.That(set.Count).IsEqualTo(3);
        await Assert.That(set.Contains(1)).IsTrue();
        await Assert.That(set.Contains(2)).IsTrue();
        await Assert.That(set.Contains(3)).IsTrue();
    }

    [Test]
    public async Task EnumerationShouldIterateInOrder()
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

        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0]).IsEqualTo(1);
        await Assert.That(items[1]).IsEqualTo(2);
        await Assert.That(items[2]).IsEqualTo(3);
    }

    [Test]
    public async Task StringItemsShouldMaintainLexicographicOrder()
    {
        var set = new TreeSet<string>
        {
            "zebra",
            "apple",
            "banana"
        };

        var items = set.ToList();

        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0]).IsEqualTo("apple");
        await Assert.That(items[1]).IsEqualTo("banana");
        await Assert.That(items[2]).IsEqualTo("zebra");
    }

    [Test]
    public async Task LargeDataSetShouldMaintainPerformance()
    {
        var set = new TreeSet<int>();

        // Add 10,000 items
        for (var i = 0; i < 10000; i++)
        {
            set.Add(i);
        }

        await Assert.That(set.Count).IsEqualTo(10000);

        // Verify some random accesses
        await Assert.That(set.Contains(0)).IsTrue();
        await Assert.That(set.Contains(5000)).IsTrue();
        await Assert.That(set.Contains(9999)).IsTrue();

        // Remove half the items
        for (var i = 0; i < 5000; i++)
        {
            set.Remove(i);
        }

        await Assert.That(set.Count).IsEqualTo(5000);
        await Assert.That(set.Contains(0)).IsFalse();
        await Assert.That(set.Contains(5000)).IsTrue();
    }

    [Test]
    public async Task EmptySetOperationsShouldHandleGracefully()
    {
        var emptySet = new TreeSet<int>();
        var otherSet = new TreeSet<int> { 1 };

        await Assert.That(emptySet.Except(otherSet).IsEmpty).IsTrue();
        await Assert.That(emptySet.Intersect(otherSet).IsEmpty).IsTrue();
        await Assert.That(emptySet.Union(otherSet).Count).IsEqualTo(1);
        await Assert.That(emptySet.SymmetricExcept(otherSet).Count).IsEqualTo(1);
        await Assert.That(emptySet.IsSubsetOf(otherSet)).IsTrue();
        await Assert.That(emptySet.IsSupersetOf(otherSet)).IsFalse();
        await Assert.That(emptySet.Overlaps(otherSet)).IsFalse();
        await Assert.That(emptySet.SetEquals(otherSet)).IsFalse();
    }

    [Test]
    public async Task SetOperationsShouldMatchReferenceImplementationOnLargerSets()
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

        await Assert.That(left.Except(right).ToList()).IsEquivalentTo(leftItems.Except(rightItems).ToList());
        await Assert.That(left.Intersect(right).ToList()).IsEquivalentTo(leftItems.Intersect(rightItems).ToList());
        await Assert.That(left.Union(right).ToList()).IsEquivalentTo(leftItems.Union(rightItems).ToList());
        await Assert.That(left.SymmetricExcept(right).ToList()).IsEquivalentTo(leftItems.Except(rightItems).Union(rightItems.Except(leftItems)).ToList());

        await Assert.That(left.IsSubsetOf(left.Union(right))).IsTrue();
        await Assert.That(left.IsSubsetOf(right)).IsFalse();
        await Assert.That(left.IsSupersetOf(left.Intersect(right))).IsTrue();
        await Assert.That(left.Overlaps(right)).IsTrue();
        await Assert.That(left.SetEquals(right)).IsFalse();
        await Assert.That(left.SetEquals([.. leftItems])).IsTrue();
    }

    [Test]
    public async Task ExtendOnLargeBatchShouldRebuildOnceAndKeepSortedOrder()
    {
        var set = new TreeSet<int> { 10 };
        var batch = new List<int>();
        for (var i = 1000; i >= 0; i--)
        {
            batch.Add(i);
        }

        set.Extend(batch);

        await Assert.That(set.Count).IsEqualTo(1001);
        await Assert.That(set.Contains(0)).IsTrue();
        await Assert.That(set.Contains(1000)).IsTrue();
        var items = new List<int>();
        foreach (var item in set)
        {
            items.Add(item);
        }

        for (var i = 1; i < items.Count; i++)
        {
            await Assert.That(items[i - 1] < items[i]).IsTrue();
        }
    }
}