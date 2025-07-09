using Microsoft.VisualStudio.TestTools.UnitTesting;
using SumTree.TreeMap;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SumTree.Tests;

[TestClass]
public class TreeMapTests
{
    [TestMethod]
    public void EmptyTreeMap_ShouldHaveZeroCount()
    {
        var map = new TreeMap<int, string>();

        Assert.IsTrue(map.IsEmpty);
        Assert.AreEqual(0, map.Count);
    }

    [TestMethod]
    public void Insert_SingleItem_ShouldBeRetrievable()
    {
        var map = new TreeMap<int, string>();

        map.Insert(1, "one");

        Assert.AreEqual("one", map.Get(1));
        Assert.AreEqual(1, map.Count);
        Assert.IsFalse(map.IsEmpty);
    }

    [TestMethod]
    public void Insert_MultipleItems_ShouldMaintainOrder()
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
    public void Insert_DuplicateKey_ShouldReplaceValue()
    {
        var map = new TreeMap<int, string>();

        map.Insert(1, "one");
        map.Insert(1, "ONE");

        Assert.AreEqual("ONE", map.Get(1));
        Assert.AreEqual(1, map.Count);
    }

    [TestMethod]
    public void Get_NonexistentKey_ShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var result = map.Get(999);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryGetValue_ExistingKey_ShouldReturnTrue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var found = map.TryGetValue(1, out var value);

        Assert.IsTrue(found);
        Assert.AreEqual("one", value);
    }

    [TestMethod]
    public void TryGetValue_NonexistentKey_ShouldReturnFalse()
    {
        var map = new TreeMap<int, string>();

        var found = map.TryGetValue(999, out var value);

        Assert.IsFalse(found);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void ContainsKey_ExistingKey_ShouldReturnTrue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        Assert.IsTrue(map.ContainsKey(1));
    }

    [TestMethod]
    public void ContainsKey_NonexistentKey_ShouldReturnFalse()
    {
        var map = new TreeMap<int, string>();

        Assert.IsFalse(map.ContainsKey(999));
    }

    [TestMethod]
    public void Remove_ExistingKey_ShouldReturnValueAndRemove()
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
    public void Remove_NonexistentKey_ShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var removed = map.Remove(999);

        Assert.IsNull(removed);
    }

    [TestMethod]
    public void Clear_ShouldRemoveAllItems()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");

        map.Clear();

        Assert.IsTrue(map.IsEmpty);
        Assert.AreEqual(0, map.Count);
    }

    [TestMethod]
    public void Extend_ShouldAddMultipleItems()
    {
        var map = new TreeMap<int, string>();
        var items = new[]
        {
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        };

        map.Extend(items);

        Assert.AreEqual(3, map.Count);
        Assert.AreEqual("one", map.Get(1));
        Assert.AreEqual("two", map.Get(2));
        Assert.AreEqual("three", map.Get(3));
    }

    [TestMethod]
    public void RemoveRange_ShouldRemoveItemsInRange()
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
    public void Closest_ShouldReturnClosestKeyValue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(3, "three");
        map.Insert(5, "five");

        var closest = map.Closest(2);

        Assert.IsNotNull(closest);
        Assert.AreEqual(1, closest.Value.Key);
        Assert.AreEqual("one", closest.Value.Value);
    }

    [TestMethod]
    public void Closest_NoSmallerKey_ShouldReturnNull()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(5, "five");

        var closest = map.Closest(1);

        Assert.IsNull(closest);
    }

    [TestMethod]
    public void IterateFrom_ShouldStartFromSpecifiedKey()
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
    public void Update_ExistingKey_ShouldReturnResult()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var result = map.Update(1, value => value.ToUpper());

        Assert.AreEqual("ONE", result);
    }

    [TestMethod]
    public void Update_NonexistentKey_ShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var result = map.Update(999, value => value.ToUpper());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Retain_ShouldKeepOnlyMatchingItems()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");
        map.Insert(3, "three");
        map.Insert(4, "four");

        map.Retain((key, value) => key % 2 == 0);

        Assert.AreEqual(2, map.Count);
        Assert.IsTrue(map.ContainsKey(2));
        Assert.IsTrue(map.ContainsKey(4));
        Assert.IsFalse(map.ContainsKey(1));
        Assert.IsFalse(map.ContainsKey(3));
    }

    [TestMethod]
    public void First_ShouldReturnFirstEntry()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var first = map.First();

        Assert.IsNotNull(first);
        Assert.AreEqual(1, first.Value.Key);
        Assert.AreEqual("one", first.Value.Value);
    }

    [TestMethod]
    public void First_EmptyMap_ShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var first = map.First();

        Assert.IsNull(first);
    }

    [TestMethod]
    public void Last_ShouldReturnLastEntry()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var last = map.Last();

        Assert.IsNotNull(last);
        Assert.AreEqual(3, last.Value.Key);
        Assert.AreEqual("three", last.Value.Value);
    }

    [TestMethod]
    public void Last_EmptyMap_ShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var last = map.Last();

        Assert.IsNull(last);
    }

    [TestMethod]
    public void InsertTree_ShouldMergeAnotherTreeMap()
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
    public void Keys_ShouldReturnAllKeys()
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
    public void Values_ShouldReturnAllValues()
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
    public void Indexer_Get_ShouldReturnValue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        Assert.AreEqual("one", map[1]);
    }

    [TestMethod]
    public void Indexer_Set_ShouldInsertValue()
    {
        var map = new TreeMap<int, string>();

        map[1] = "one";

        Assert.AreEqual("one", map.Get(1));
    }

    [TestMethod]
    public void FromOrderedEntries_ShouldCreateMapFromOrderedData()
    {
        var entries = new[]
        {
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        };

        var map = new TreeMap<int, string>(entries);

        Assert.AreEqual(3, map.Count);
        Assert.AreEqual("one", map.Get(1));
        Assert.AreEqual("two", map.Get(2));
        Assert.AreEqual("three", map.Get(3));
    }

    [TestMethod]
    public void Enumeration_ShouldIterateInOrder()
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
    public void StringKeys_ShouldMaintainLexicographicOrder()
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
    public void LargeDataSet_ShouldMaintainPerformance()
    {
        var map = new TreeMap<int, string>();

        // Insert 10,000 items
        for (int i = 0; i < 10000; i++)
        {
            map.Insert(i, $"value_{i}");
        }

        Assert.AreEqual(10000, map.Count);

        // Verify some random accesses
        Assert.AreEqual("value_0", map.Get(0));
        Assert.AreEqual("value_5000", map.Get(5000));
        Assert.AreEqual("value_9999", map.Get(9999));

        // Remove half the items
        for (int i = 0; i < 5000; i++)
        {
            map.Remove(i);
        }

        Assert.AreEqual(5000, map.Count);
        Assert.IsNull(map.Get(0));
        Assert.AreEqual("value_5000", map.Get(5000));
    }
}
