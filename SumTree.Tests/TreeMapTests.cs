
namespace SumTree.Tests;
public class TreeMapTests
{
    [Test]
    public async Task EmptyTreeMapShouldHaveZeroCount()
    {
        var map = new TreeMap<int, string>();

        await Assert.That(map.IsEmpty).IsTrue();
        await Assert.That(map.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InsertSingleItemShouldBeRetrievable()
    {
        var map = new TreeMap<int, string>();

        map.Insert(1, "one");

        await Assert.That(map.Get(1)).IsEqualTo("one");
        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map.IsEmpty).IsFalse();
    }

    [Test]
    public async Task InsertMultipleItemsShouldMaintainOrder()
    {
        var map = new TreeMap<int, string>();

        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var items = map.ToList();
        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0].Key).IsEqualTo(1);
        await Assert.That(items[1].Key).IsEqualTo(2);
        await Assert.That(items[2].Key).IsEqualTo(3);
    }

    [Test]
    public async Task InsertDuplicateKeyShouldReplaceValue()
    {
        var map = new TreeMap<int, string>();

        map.Insert(1, "one");
        map.Insert(1, "ONE");

        await Assert.That(map.Get(1)).IsEqualTo("ONE");
        await Assert.That(map.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetNonexistentKeyShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var result = map.Get(999);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryGetValueExistingKeyShouldReturnTrue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var found = map.TryGetValue(1, out var value);

        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo("one");
    }

    [Test]
    public async Task TryGetValueNonexistentKeyShouldReturnFalse()
    {
        var map = new TreeMap<int, string>();

        var found = map.TryGetValue(999, out var value);

        await Assert.That(found).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task ContainsKeyExistingKeyShouldReturnTrue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        await Assert.That(map.ContainsKey(1)).IsTrue();
    }

    [Test]
    public async Task ContainsKeyNonexistentKeyShouldReturnFalse()
    {
        var map = new TreeMap<int, string>();

        await Assert.That(map.ContainsKey(999)).IsFalse();
    }

    [Test]
    public async Task RemoveExistingKeyShouldReturnValueAndRemove()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");

        var removed = map.Remove(1);

        await Assert.That(removed).IsEqualTo("one");
        await Assert.That(map.ContainsKey(1)).IsFalse();
        await Assert.That(map.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveNonexistentKeyShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var removed = map.Remove(999);

        await Assert.That(removed).IsNull();
    }

    [Test]
    public async Task ClearShouldRemoveAllItems()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");

        map.Clear();

        await Assert.That(map.IsEmpty).IsTrue();
        await Assert.That(map.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExtendShouldAddMultipleItems()
    {
        var map = new TreeMap<int, string>();
        KeyValuePair<int, string>[] items =
        [
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        ];

        map.Extend(items);

        await Assert.That(map.Count).IsEqualTo(3);
        await Assert.That(map.Get(1)).IsEqualTo("one");
        await Assert.That(map.Get(2)).IsEqualTo("two");
        await Assert.That(map.Get(3)).IsEqualTo("three");
    }

    [Test]
    public async Task RemoveRangeShouldRemoveItemsInRange()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");
        map.Insert(3, "three");
        map.Insert(4, "four");
        map.Insert(5, "five");

        map.RemoveRange(2, 4); // Remove keys 2 and 3

        await Assert.That(map.Count).IsEqualTo(3);
        await Assert.That(map.ContainsKey(1)).IsTrue();
        await Assert.That(map.ContainsKey(2)).IsFalse();
        await Assert.That(map.ContainsKey(3)).IsFalse();
        await Assert.That(map.ContainsKey(4)).IsTrue();
        await Assert.That(map.ContainsKey(5)).IsTrue();
    }

    [Test]
    public async Task ClosestShouldReturnClosestKeyValue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(3, "three");
        map.Insert(5, "five");

        var closest = map.Closest(2);

        await Assert.That(closest.HasValue).IsTrue();
        await Assert.That(closest.Value.Key).IsEqualTo(1);
        await Assert.That(closest.Value.Value).IsEqualTo("one");
    }

    [Test]
    public async Task ClosestNoSmallerKeyShouldReturnEmpty()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(5, "five");

        var closest = map.Closest(1);

        await Assert.That(closest.HasValue).IsFalse();
    }

    [Test]
    public async Task IterateFromShouldStartFromSpecifiedKey()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");
        map.Insert(3, "three");
        map.Insert(4, "four");

        var items = map.IterateFrom(2).ToList();

        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0].Key).IsEqualTo(2);
        await Assert.That(items[1].Key).IsEqualTo(3);
        await Assert.That(items[2].Key).IsEqualTo(4);
    }

    [Test]
    public async Task UpdateExistingKeyShouldReturnResult()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var result = map.Update(1, value => value.ToUpper());

        await Assert.That(result).IsEqualTo("ONE");
    }

    [Test]
    public async Task UpdateNonexistentKeyShouldReturnNull()
    {
        var map = new TreeMap<int, string>();

        var result = map.Update(999, value => value.ToUpper());

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task UpdateShouldWriteBackNewValue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var result = map.Update(1, value => value.ToUpper());

        await Assert.That(result).IsEqualTo("ONE");
        await Assert.That(map.Get(1)).IsEqualTo("ONE");
    }

    [Test]
    public async Task UpdateNonexistentKeyShouldNotChangeMap()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        map.Update(999, value => value.ToUpper());

        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map.Get(1)).IsEqualTo("one");
    }

    [Test]
    public async Task RetainShouldKeepOnlyMatchingItems()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");
        map.Insert(2, "two");
        map.Insert(3, "three");
        map.Insert(4, "four");

        map.Retain((key, _) => key % 2 == 0);

        await Assert.That(map.Count).IsEqualTo(2);
        await Assert.That(map.ContainsKey(2)).IsTrue();
        await Assert.That(map.ContainsKey(4)).IsTrue();
        await Assert.That(map.ContainsKey(1)).IsFalse();
        await Assert.That(map.ContainsKey(3)).IsFalse();
    }

    [Test]
    public async Task FirstShouldReturnFirstEntry()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var first = map.First();

        await Assert.That(first.HasValue).IsTrue();
        await Assert.That(first.Value.Key).IsEqualTo(1);
        await Assert.That(first.Value.Value).IsEqualTo("one");
    }

    [Test]
    public async Task FirstEmptyMapShouldReturnEmpty()
    {
        var map = new TreeMap<int, string>();

        var first = map.First();

        await Assert.That(first.HasValue).IsFalse();
    }

    [Test]
    public async Task LastShouldReturnLastEntry()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var last = map.Last();

        await Assert.That(last.HasValue).IsTrue();
        await Assert.That(last.Value.Key).IsEqualTo(3);
        await Assert.That(last.Value.Value).IsEqualTo("three");
    }

    [Test]
    public async Task LastEmptyMapShouldReturnEmpty()
    {
        var map = new TreeMap<int, string>();

        var last = map.Last();

        await Assert.That(last.HasValue).IsFalse();
    }

    [Test]
    public async Task InsertTreeShouldMergeAnotherTreeMap()
    {
        var map1 = new TreeMap<int, string>();
        map1.Insert(1, "one");
        map1.Insert(2, "two");

        var map2 = new TreeMap<int, string>();
        map2.Insert(2, "TWO"); // Should overwrite
        map2.Insert(3, "three");

        map1.InsertTree(map2);

        await Assert.That(map1.Count).IsEqualTo(3);
        await Assert.That(map1.Get(1)).IsEqualTo("one");
        await Assert.That(map1.Get(2)).IsEqualTo("TWO");
        await Assert.That(map1.Get(3)).IsEqualTo("three");
    }

    [Test]
    public async Task KeysShouldReturnAllKeys()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var keys = map.Keys.ToList();

        await Assert.That(keys.Count).IsEqualTo(3);
        await Assert.That(keys[0]).IsEqualTo(1);
        await Assert.That(keys[1]).IsEqualTo(2);
        await Assert.That(keys[2]).IsEqualTo(3);
    }

    [Test]
    public async Task ValuesShouldReturnAllValues()
    {
        var map = new TreeMap<int, string>();
        map.Insert(3, "three");
        map.Insert(1, "one");
        map.Insert(2, "two");

        var values = map.Values.ToList();

        await Assert.That(values.Count).IsEqualTo(3);
        await Assert.That(values[0]).IsEqualTo("one");
        await Assert.That(values[1]).IsEqualTo("two");
        await Assert.That(values[2]).IsEqualTo("three");
    }

    [Test]
    public async Task IndexerGetShouldReturnValue()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        await Assert.That(map[1]).IsEqualTo("one");
    }

    [Test]
    public async Task IndexerGetShouldThrowForMissingKey()
    {
        var map = new TreeMap<int, string>();

        await Assert.That(() => map[999]).ThrowsExactly<KeyNotFoundException>();
        await Assert.That(map.Get(999)).IsNull();
    }

    [Test]
    public async Task SnapshotShouldBeUnaffectedByLaterMutations()
    {
        var map = new TreeMap<int, string>();
        map.Insert(1, "one");

        var snapshot = map.Snapshot();

        map.Insert(2, "two");
        map.Insert(1, "uno");

        await Assert.That(snapshot.Count).IsEqualTo(1);
        await Assert.That(snapshot.Get(1)).IsEqualTo("one");
        await Assert.That(snapshot.ContainsKey(2)).IsFalse();
        await Assert.That(map.Count).IsEqualTo(2);
        await Assert.That(map.Get(1)).IsEqualTo("uno");

        map.TryRemove(1, out _);

        await Assert.That(snapshot.Count).IsEqualTo(1);
        await Assert.That(snapshot.Get(1)).IsEqualTo("one");
        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map.Get(1)).IsNull();
        await Assert.That(map.ContainsKey(2)).IsTrue();
    }

    [Test]
    public async Task IndexerSetShouldInsertValue()
    {
        var map = new TreeMap<int, string>();

        map[1] = "one";

        await Assert.That(map.Get(1)).IsEqualTo("one");
    }

    [Test]
    public async Task FromOrderedEntriesShouldCreateMapFromOrderedData()
    {
        KeyValuePair<int, string>[] entries =
        [
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three")
        ];

        var map = new TreeMap<int, string>(entries);

        await Assert.That(map.Count).IsEqualTo(3);
        await Assert.That(map.Get(1)).IsEqualTo("one");
        await Assert.That(map.Get(2)).IsEqualTo("two");
        await Assert.That(map.Get(3)).IsEqualTo("three");
    }

    [Test]
    public async Task EnumerationShouldIterateInOrder()
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

        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That(items[0].Key).IsEqualTo(1);
        await Assert.That(items[1].Key).IsEqualTo(2);
        await Assert.That(items[2].Key).IsEqualTo(3);
    }

    [Test]
    public async Task StringKeysShouldMaintainLexicographicOrder()
    {
        var map = new TreeMap<string, int>();
        map.Insert("zebra", 1);
        map.Insert("apple", 2);
        map.Insert("banana", 3);

        var keys = map.Keys.ToList();

        await Assert.That(keys.Count).IsEqualTo(3);
        await Assert.That(keys[0]).IsEqualTo("apple");
        await Assert.That(keys[1]).IsEqualTo("banana");
        await Assert.That(keys[2]).IsEqualTo("zebra");
    }

    [Test]
    public async Task LargeDataSetShouldMaintainPerformance()
    {
        var map = new TreeMap<int, string>();

        // Insert 10,000 items
        for (var i = 0; i < 10000; i++)
        {
            map.Insert(i, $"value_{i}");
        }

        await Assert.That(map.Count).IsEqualTo(10000);

        // Verify some random accesses
        await Assert.That(map.Get(0)).IsEqualTo("value_0");
        await Assert.That(map.Get(5000)).IsEqualTo("value_5000");
        await Assert.That(map.Get(9999)).IsEqualTo("value_9999");

        // Remove half the items
        for (var i = 0; i < 5000; i++)
        {
            map.Remove(i);
        }

        await Assert.That(map.Count).IsEqualTo(5000);
        await Assert.That(map.Get(0)).IsNull();
        await Assert.That(map.Get(5000)).IsEqualTo("value_5000");
    }

    [Test]
    public async Task MapKeySummaryShouldReportGreatestKeyWhenAllKeysAreNegative()
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

        await Assert.That(summary.IsEmpty).IsFalse();
        await Assert.That(summary.Key).IsEqualTo(-1);
        await Assert.That(MapKeySummaryOperations<int, string>.SummarizeSpan(sorted)).IsEqualTo(summary);
        await Assert.That(MapKeySummaryOperations<int, string>.Identity.CompareTo(summary) < 0).IsTrue();
    }

    [Test]
    public async Task MapKeyEmptyShouldBeEmptyForValueTypeKeys()
    {
        var empty = MapKey<int>.Empty;
        var zero = new MapKey<int>(0);

        await Assert.That(empty.IsEmpty).IsTrue();
        await Assert.That(zero.IsEmpty).IsFalse();
        await Assert.That(empty != zero).IsTrue();
        await Assert.That(empty < zero).IsTrue();
    }

    [Test]
    public async Task FromUnorderedEntriesShouldThrow()
    {
        KeyValuePair<int, string>[] entries =
        [
            new KeyValuePair<int, string>(3, "three"),
            new KeyValuePair<int, string>(1, "one")
        ];

        await Assert.That(() => new TreeMap<int, string>(entries)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task FromDuplicateEntriesShouldThrow()
    {
        KeyValuePair<int, string>[] entries =
        [
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(1, "duplicate")
        ];

        await Assert.That(() => new TreeMap<int, string>(entries)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ExtendWithConflictingKeysShouldUseIncomingValues()
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

        await Assert.That(map.Count).IsEqualTo(3);
        await Assert.That(map.Get(2)).IsEqualTo("TWO");
        await Assert.That(map.Get(3)).IsEqualTo("three");
    }

    [Test]
    public async Task ExtendLargeBatchShouldMergeInOnePass()
    {
        var map = new TreeMap<int, string>();
        map.Insert(5, "five");

        var batch = new List<KeyValuePair<int, string>>();
        for (var i = 0; i < 100; i++)
        {
            batch.Add(new KeyValuePair<int, string>(i, $"value_{i}"));
        }

        map.Extend(batch);

        await Assert.That(map.Count).IsEqualTo(100);
        await Assert.That(map.Get(5)).IsEqualTo("value_5");
        await Assert.That(map.Get(99)).IsEqualTo("value_99");
        for (var i = 1; i < 100; i++)
        {
            await Assert.That(map.ContainsKey(i)).IsTrue();
        }
    }

    [Test]
    public async Task RetainShouldKeepLookupsWorking()
    {
        var map = new TreeMap<int, string>();
        for (var i = 0; i < 100; i++)
        {
            map.Insert(i, $"value_{i}");
        }

        map.Retain((key, _) => key % 2 == 0);

        await Assert.That(map.Count).IsEqualTo(50);
        await Assert.That(map.ContainsKey(42)).IsTrue();
        await Assert.That(map.ContainsKey(43)).IsFalse();
        await Assert.That(map.Get(98)).IsEqualTo("value_98");
    }
}