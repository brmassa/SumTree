using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SumTree.TreeMap;

namespace SumTree.Tests;

[TestClass]
public class TreeMapTests
{
    [TestMethod]
    public void EmptyTreeMapShouldHaveZeroCount()
    {
        var map = new TreeMap<int, string>();

        Assert.IsTrue(map.IsEmpty);
        Assert.AreEqual(0, map.Count);
    }

    [TestMethod]
    public void InsertSingleItemShouldBeRetrievable()
    {
        var map = new TreeMap<int, string>();

        map.Insert(1, "one");

        Assert.AreEqual("one", map.Get(1));
        Assert.AreEqual(1, map.Count);
        Assert.IsFalse(map.IsEmpty);
    }

    [TestMethod]
    public void InsertMultipleItemsShouldMaintainOrder()
    {
        var map = new TreeMap<int, string>();

        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var items = map.ToList();
        Assert.AreEqual(3, items.Count);
        Assert.AreEqual(1, items[0].Key);
        Assert.AreEqual(2, items[1].Key);
        Assert.AreEqual(3, items[2].Key);
    }

    [TestMethod]
    public void InsertDuplicateKeyShouldReplaceValue()
    {
        var map = new TreeMap<int, string>();

        map.Insert(1, "one");
        map.Insert(1, "ONE");

        Assert.AreEqual("ONE", map.Get(1));
        Assert.AreEqual(1, map.Count);
    }

    [TestMethod]
    public void GetNonexistentKeyShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var result = map.Get(999);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryGetValueExistingKeyShouldReturnTrue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var found = map.TryGetValue(1, out var value);

        Assert.IsTrue(found);
        Assert.AreEqual("one", value);
    }

    [TestMethod]
    public void TryGetValueNonexistentKeyShouldReturnFalse()
    {
        var map = new TreeMap<int, string>();

        var found = map.TryGetValue(999, out var value);

        Assert.IsFalse(found);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void ContainsKeyExistingKeyShouldReturnTrue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        Assert.IsTrue(map.ContainsKey(1));
    }

    [TestMethod]
    public void ContainsKeyNonexistentKeyShouldReturnFalse()
    {
        var map = new TreeMap<int, string>();

        Assert.IsFalse(map.ContainsKey(999));
    }

    [TestMethod]
    public void RemoveExistingKeyShouldReturnValueAndRemove()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");

        var removed = map.Remove(1);

        Assert.AreEqual("one", removed);
        Assert.IsFalse(map.ContainsKey(1));
        Assert.AreEqual(1, map.Count);
    }

    [TestMethod]
    public void RemoveNonexistentKeyShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var removed = map.Remove(999);

        Assert.IsNull(removed);
    }

    [TestMethod]
    public void ClearShouldRemoveAllItems()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");

        map.Clear();

        Assert.IsTrue(map.IsEmpty);
        Assert.AreEqual(0, map.Count);
    }

    [TestMethod]
    public void ExtendShouldAddMultipleItems()
    {
        var map = new TreeMap<int, string>();
        KeyValuePair<int, string>[] items =
        [
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        ];

        map.Extend(items);

        Assert.AreEqual(3, map.Count);
        Assert.AreEqual("one", map.Get(1));
        Assert.AreEqual("two", map.Get(2));
        Assert.AreEqual("three", map.Get(3));
    }

    [TestMethod]
    public void RemoveRangeShouldRemoveItemsInRange()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");
        map.Insert(3, "three");
        map.Insert(4, "four");
        map.Insert(5, "five");

        map.RemoveRange(2, 4); // Remove keys 2 and 3

        Assert.AreEqual(3, map.Count);
        Assert.IsTrue(map.ContainsKey(1));
        Assert.IsFalse(map.ContainsKey(2));
        Assert.IsFalse(map.ContainsKey(3));
        Assert.IsTrue(map.ContainsKey(4));
        Assert.IsTrue(map.ContainsKey(5));
    }

    [TestMethod]
    public void ClosestShouldReturnClosestKeyValue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(3, "three");
        map.Insert(5, "five");

        var closest = map.Closest(2);

        Assert.IsTrue(closest.HasValue);
        Assert.AreEqual(1, closest.Value.Key);
        Assert.AreEqual("one", closest.Value.Value);
    }

    [TestMethod]
    public void ClosestNoSmallerKeyShouldReturnEmpty()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(5, "five");

        var closest = map.Closest(1);

        Assert.IsFalse(closest.HasValue);
    }

    [TestMethod]
    public void IterateFromShouldStartFromSpecifiedKey()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");
        map.Insert(3, "three");
        map.Insert(4, "four");

        var items = map.IterateFrom(2).ToList();

        Assert.AreEqual(3, items.Count);
        Assert.AreEqual(2, items[0].Key);
        Assert.AreEqual(3, items[1].Key);
        Assert.AreEqual(4, items[2].Key);
    }

    [TestMethod]
    public void UpdateExistingKeyShouldReturnResult()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var result = map.Update(1, value => value.ToUpper());

        Assert.AreEqual("ONE", result);
    }

    [TestMethod]
    public void UpdateNonexistentKeyShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var result = map.Update(999, value => value.ToUpper());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void UpdateShouldWriteBackNewValue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var result = map.Update(1, value => value.ToUpper());

        Assert.AreEqual("ONE", result);
        Assert.AreEqual("ONE", map.Get(1));
    }

    [TestMethod]
    public void UpdateNonexistentKeyShouldNotChangeMap()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        map.Update(999, value => value.ToUpper());

        Assert.AreEqual(1, map.Count);
        Assert.AreEqual("one", map.Get(1));
    }

    [TestMethod]
    public void RetainShouldKeepOnlyMatchingItems()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");
        map.Insert(3, "three");
        map.Insert(4, "four");

        map.Retain((key, _) => key % 2 == 0);

        Assert.AreEqual(2, map.Count);
        Assert.IsTrue(map.ContainsKey(2));
        Assert.IsTrue(map.ContainsKey(4));
        Assert.IsFalse(map.ContainsKey(1));
        Assert.IsFalse(map.ContainsKey(3));
    }

    [TestMethod]
    public void FirstShouldReturnFirstEntry()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var first = map.First();

        Assert.IsTrue(first.HasValue);
        Assert.AreEqual(1, first.Value.Key);
        Assert.AreEqual("one", first.Value.Value);
    }

    [TestMethod]
    public void FirstEmptyMapShouldReturnEmpty()
    {
        var map = new TreeMap<int, string>();

        var first = map.First();

        Assert.IsFalse(first.HasValue);
    }

    [TestMethod]
    public void LastShouldReturnLastEntry()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var last = map.Last();

        Assert.IsTrue(last.HasValue);
        Assert.AreEqual(3, last.Value.Key);
        Assert.AreEqual("three", last.Value.Value);
    }

    [TestMethod]
    public void LastEmptyMapShouldReturnEmpty()
    {
        var map = new TreeMap<int, string>();

        var last = map.Last();

        Assert.IsFalse(last.HasValue);
    }

    [TestMethod]
    public void InsertTreeShouldMergeAnotherTreeMap()
    {
        var map1 = new TreeMap<int, string>();
        map1.Insert(1, "one");
        map1.Insert(2, "two");

        var map2 = new TreeMap<int, string>();
        map2.Insert(2, "TWO"); // Should overwrite
        map2.Insert(3, "three");

        map1.InsertTree(map2);

        Assert.AreEqual(3, map1.Count);
        Assert.AreEqual("one", map1.Get(1));
        Assert.AreEqual("TWO", map1.Get(2));
        Assert.AreEqual("three", map1.Get(3));
    }

    [TestMethod]
    public void KeysShouldReturnAllKeys()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var keys = map.Keys.ToList();

        Assert.AreEqual(3, keys.Count);
        Assert.AreEqual(1, keys[0]);
        Assert.AreEqual(2, keys[1]);
        Assert.AreEqual(3, keys[2]);
    }

    [TestMethod]
    public void ValuesShouldReturnAllValues()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var values = map.Values.ToList();

        Assert.AreEqual(3, values.Count);
        Assert.AreEqual("one", values[0]);
        Assert.AreEqual("two", values[1]);
        Assert.AreEqual("three", values[2]);
    }

    [TestMethod]
    public void IndexerGetShouldReturnValue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        Assert.AreEqual("one", map[1]);
    }

    [TestMethod]
    public void IndexerGetShouldThrowForMissingKey()
    {
        var map = new TreeMap<int, string>();

        Assert.ThrowsExactly<KeyNotFoundException>(() => map[999]);
        Assert.AreEqual(default, map.Get(999));
    }

    [TestMethod]
    public void SnapshotShouldBeUnaffectedByLaterMutations()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var snapshot = map.Snapshot();

        map.Insert(2, "two");
        map.Insert(1, "uno");

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual("one", snapshot.Get(1));
        Assert.IsFalse(snapshot.ContainsKey(2));
        Assert.AreEqual(2, map.Count);
        Assert.AreEqual("uno", map.Get(1));

        map.TryRemove(1, out _);

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual("one", snapshot.Get(1));
        Assert.AreEqual(1, map.Count);
        Assert.IsNull(map.Get(1));
        Assert.IsTrue(map.ContainsKey(2));
    }

    [TestMethod]
    public void IndexerSetShouldInsertValue()
    {
        var map = new TreeMap<int, string>();

        map[1] = "one";

        Assert.AreEqual("one", map.Get(1));
    }

    [TestMethod]
    public void FromOrderedEntriesShouldCreateMapFromOrderedData()
    {
        KeyValuePair<int, string>[] entries =
        [
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        ];

        var map = new TreeMap<int, string>(entries);

        Assert.AreEqual(3, map.Count);
        Assert.AreEqual("one", map.Get(1));
        Assert.AreEqual("two", map.Get(2));
        Assert.AreEqual("three", map.Get(3));
    }

    [TestMethod]
    public void EnumerationShouldIterateInOrder()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var items = new List<KeyValuePair<int, string>>();
        foreach (var item in map)
        {
            items.Add(item);
        }

        Assert.AreEqual(3, items.Count);
        Assert.AreEqual(1, items[0].Key);
        Assert.AreEqual(2, items[1].Key);
        Assert.AreEqual(3, items[2].Key);
    }

    [TestMethod]
    public void StringKeysShouldMaintainLexicographicOrder()
    {
        var map = new TreeMap<string, int>();
        map.Insert("zebra", 1);
        map.Insert("apple", 2);
        map.Insert("banana", 3);

        var keys = map.Keys.ToList();

        Assert.AreEqual(3, keys.Count);
        Assert.AreEqual("apple", keys[0]);
        Assert.AreEqual("banana", keys[1]);
        Assert.AreEqual("zebra", keys[2]);
    }

    [TestMethod]
    public void LargeDataSetShouldMaintainPerformance()
    {
        var map = new TreeMap<int, string>();

        // Insert 10,000 items
        for (var i = 0; i < 10000; i++)
        {
            map.Insert(i, $"value_{i}");
        }

        Assert.AreEqual(10000, map.Count);

        // Verify some random accesses
        Assert.AreEqual("value_0", map.Get(0));
        Assert.AreEqual("value_5000", map.Get(5000));
        Assert.AreEqual("value_9999", map.Get(9999));

        // Remove half the items
        for (var i = 0; i < 5000; i++)
        {
            map.Remove(i);
        }

        Assert.AreEqual(5000, map.Count);
        Assert.IsNull(map.Get(0));
        Assert.AreEqual("value_5000", map.Get(5000));
    }

    [TestMethod]
    public void MapKeySummaryShouldReportGreatestKeyWhenAllKeysAreNegative()
    {
        var sorted = new MapEntry<int, string>[]
        {
            new(-5, "a"), new(-3, "c"), new(-1, "b")
        };

        var summary = MapKeySummaryOperations<int, string>.Combine(
            MapKeySummaryOperations<int, string>.Combine(
                MapKeySummaryOperations<int, string>.SummarizeElement(sorted[0]),
                MapKeySummaryOperations<int, string>.SummarizeElement(sorted[1])),
            MapKeySummaryOperations<int, string>.SummarizeElement(sorted[2]));

        Assert.IsFalse(summary.IsEmpty);
        Assert.AreEqual(-1, summary.Key);
        Assert.AreEqual(summary, MapKeySummaryOperations<int, string>.SummarizeSpan(sorted));
        Assert.IsTrue(MapKeySummaryOperations<int, string>.Identity.CompareTo(summary) < 0);
    }

    [TestMethod]
    public void MapKeyEmptyShouldBeEmptyForValueTypeKeys()
    {
        var empty = MapKey<int>.Empty;
        var zero = new MapKey<int>(0);

        Assert.IsTrue(empty.IsEmpty);
        Assert.IsFalse(zero.IsEmpty);
        Assert.IsTrue(empty != zero);
        Assert.IsTrue(empty < zero);
    }

    [TestMethod]
    public void FromUnorderedEntriesShouldThrow()
    {
        KeyValuePair<int, string>[] entries =
        [
            new KeyValuePair<int, string>(3, "three"),
            new KeyValuePair<int, string>(1, "one")
        ];

        Assert.ThrowsExactly<ArgumentException>(() => new TreeMap<int, string>(entries));
    }

    [TestMethod]
    public void FromDuplicateEntriesShouldThrow()
    {
        KeyValuePair<int, string>[] entries =
        [
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(1, "duplicate")
        ];

        Assert.ThrowsExactly<ArgumentException>(() => new TreeMap<int, string>(entries));
    }

    [TestMethod]
    public void ExtendWithConflictingKeysShouldUseIncomingValues()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");

        KeyValuePair<int, string>[] pairs =
        [
            new KeyValuePair<int, string>(2, "TWO"),
            new KeyValuePair<int, string>(3, "three")
        ];

        map.Extend(pairs);

        Assert.AreEqual(3, map.Count);
        Assert.AreEqual("TWO", map.Get(2));
        Assert.AreEqual("three", map.Get(3));
    }

    [TestMethod]
    public void ExtendLargeBatchShouldMergeInOnePass()
    {
        var map = new TreeMap<int, string>();
        map.Insert(5, "five");

        var batch = new List<KeyValuePair<int, string>>();
        for (var i = 0; i < 100; i++)
        {
            batch.Add(new KeyValuePair<int, string>(i, $"value_{i}"));
        }

        map.Extend(batch);

        Assert.AreEqual(100, map.Count);
        Assert.AreEqual("value_5", map.Get(5));
        Assert.AreEqual("value_99", map.Get(99));
        for (var i = 1; i < 100; i++)
        {
            Assert.IsTrue(map.ContainsKey(i));
        }
    }

    [TestMethod]
    public void RetainShouldKeepLookupsWorking()
    {
        var map = new TreeMap<int, string>();
        for (var i = 0; i < 100; i++)
        {
            map.Insert(i, $"value_{i}");
        }

        map.Retain((key, _) => key % 2 == 0);

        Assert.AreEqual(50, map.Count);
        Assert.IsTrue(map.ContainsKey(42));
        Assert.IsFalse(map.ContainsKey(43));
        Assert.AreEqual("value_98", map.Get(98));
    }
}
