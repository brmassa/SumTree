using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace SumTree.TreeMap;

/// <summary>
/// A TreeMap is a sorted map data structure built on top of the typed SumTree.
/// It provides O(log n) insertion, deletion, and lookup operations.
/// </summary>
/// <typeparam name="TKey">The type of keys in the map.</typeparam>
/// <typeparam name="TValue">The type of values in the map.</typeparam>
public class TreeMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : IComparable<TKey>, IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    SumTree<MapEntry<TKey, TValue>, MapKey<TKey>, MapKeySummaryOperations<TKey, TValue>> _tree;

    /// <summary>
    /// Initializes a new empty TreeMap.
    /// </summary>
    public TreeMap()
    {
    }

    TreeMap(SumTree<MapEntry<TKey, TValue>, MapKey<TKey>, MapKeySummaryOperations<TKey, TValue>> tree)
    {
        _tree = tree;
    }

    /// <summary>
    /// Returns a snapshot of the map in O(1). The snapshot shares this map's immutable
    /// tree and is unaffected by later mutations of this instance.
    /// </summary>
    public TreeMap<TKey, TValue> Snapshot() => new(_tree);

    /// <summary>
    /// Initializes a new TreeMap from ordered entries.
    /// </summary>
    /// <param name="orderedEntries">The ordered entries to initialize with.</param>
    public TreeMap(IEnumerable<KeyValuePair<TKey, TValue>> orderedEntries)
    {
        ArgumentNullException.ThrowIfNull(orderedEntries);
        var entries = orderedEntries.Select(kvp => new MapEntry<TKey, TValue>(kvp.Key, kvp.Value)).ToList();

        for (var i = 1; i < entries.Count; i++)
        {
            var comparison = entries[i - 1].Key.CompareTo(entries[i].Key);
            if (comparison > 0)
            {
                throw new ArgumentException(
                    "Entries must be sorted by key in ascending order.", nameof(orderedEntries));
            }

            if (comparison == 0)
            {
                throw new ArgumentException(
                    "Entries must not contain duplicate keys.", nameof(orderedEntries));
            }
        }

        _tree = Build(entries);
    }

    /// <summary>
    /// Gets whether the map is empty.
    /// </summary>
    public bool IsEmpty => _tree.IsEmpty;

    /// <summary>
    /// Gets the number of entries in the map.
    /// </summary>
    public int Count => (int)Math.Min(_tree.Length, int.MaxValue);

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value associated with the key.</returns>
    /// <exception cref="KeyNotFoundException">The key was not found in the map.</exception>
    public TValue this[TKey key]
    {
        get
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException($"The key '{key}' was not found in the map.");
        }
        set => Insert(key, value);
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value if found, otherwise the default value.</returns>
    public TValue? Get(TKey key)
    {
        var tree = _tree;
        var index = FindKeyIndex(tree, key);
        if (index >= 0)
        {
            return tree[index].Value;
        }

        return default;
    }

    /// <summary>
    /// Tries to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">When this method returns, contains the value associated with the key, if found.</param>
    /// <returns>True if the key was found, false otherwise.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var tree = _tree;
        var index = FindKeyIndex(tree, key);
        if (index >= 0)
        {
            value = tree[index].Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Determines whether the map contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>True if the map contains the key, false otherwise.</returns>
    public bool ContainsKey(TKey key)
    {
        return FindKeyIndex(_tree, key) >= 0;
    }

    /// <summary>
    /// Inserts or updates a key-value pair in the map.
    /// </summary>
    /// <param name="key">The key to insert or update.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>True if a new key was inserted, false if an existing key's value was replaced.</returns>
    public bool Insert(TKey key, TValue value)
    {
        var entry = new MapEntry<TKey, TValue>(key, value);
        var tree = _tree;
        var index = FindInsertionIndex(tree, key);

        // Check if key already exists
        if (index < tree.Length)
        {
            var existingEntry = tree[index];
            if (existingEntry.Key.Equals(key))
            {
                // Replace existing entry
                _tree = tree.RemoveAt(index).Insert(index, entry);
                return false;
            }
        }

        // Insert new entry
        _tree = tree.Insert(index, entry);
        return true;
    }

    /// <summary>
    /// Removes the entry with the specified key.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>The value that was removed, or default if the key was not found.</returns>
    public TValue? Remove(TKey key)
    {
        return TryRemove(key, out var value) ? value : default;
    }

    /// <summary>
    /// Removes the entry with the specified key, reporting whether it existed.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="value">When this method returns, contains the value that was removed.</param>
    /// <returns>True if the key was found and removed, false otherwise.</returns>
    public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var tree = _tree;
        var index = FindKeyIndex(tree, key);
        if (index >= 0)
        {
            value = tree[index].Value;
            _tree = tree.RemoveAt(index);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Removes all entries from the map.
    /// </summary>
    public void Clear()
    {
        _tree = default;
    }

    /// <summary>
    /// Extends the map with the specified key-value pairs.
    /// On key conflicts the incoming value wins.
    /// </summary>
    /// <param name="pairs">The key-value pairs to add.</param>
    public void Extend(IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        var materialized = pairs as ICollection<KeyValuePair<TKey, TValue>> ?? [.. pairs];
        if (materialized.Count == 0)
        {
            return;
        }

        if (IsEmpty || materialized.Count > Count)
        {
            // Rebuilding once from a sorted merge is cheaper than inserting a large batch element by element.
            var merged = this.Concat(materialized);
            _tree = BuildFromUnordered(merged);
            return;
        }

        foreach (var pair in materialized)
        {
            Insert(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// Removes entries in the specified key range.
    /// </summary>
    /// <param name="startKey">The start key (inclusive).</param>
    /// <param name="endKey">The end key (exclusive).</param>
    public void RemoveRange(TKey startKey, TKey endKey)
    {
        var tree = _tree;
        var startIndex = FindInsertionIndex(tree, startKey);
        var endIndex = FindInsertionIndex(tree, endKey);

        if (startIndex < endIndex)
        {
            _tree = tree.RemoveRange(startIndex, endIndex - startIndex);
        }
    }

    /// <summary>
    /// Finds the entry with the greatest key less than or equal to the specified key.
    /// </summary>
    /// <param name="key">The key to search for.</param>
    /// <returns>The closest entry, or an empty Maybe if no smaller key exists.</returns>
    public Maybe<KeyValuePair<TKey, TValue>> Closest(TKey key)
    {
        var tree = _tree;
        var index = FindInsertionIndex(tree, key);
        if (index > 0)
        {
            var entry = tree[index - 1];
            return new Maybe<KeyValuePair<TKey, TValue>>(
                new KeyValuePair<TKey, TValue>(entry.Key, entry.Value));
        }

        return Maybe<KeyValuePair<TKey, TValue>>.None;
    }

    /// <summary>
    /// Returns an iterator starting from the specified key.
    /// </summary>
    /// <param name="fromKey">The key to start iteration from.</param>
    /// <returns>An enumerator that starts from the specified key.</returns>
    public IEnumerable<KeyValuePair<TKey, TValue>> IterateFrom(TKey fromKey)
    {
        var tree = _tree;
        var startIndex = FindInsertionIndex(tree, fromKey);
        if (startIndex >= tree.Length)
        {
            yield break;
        }

        // The slice's own enumerator is a ref struct that cannot cross a yield boundary;
        // the interface enumerator keeps iteration lazy without materializing the suffix.
        IEnumerable<MapEntry<TKey, TValue>> slice = tree.Slice(startIndex, tree.Length - startIndex);
        foreach (var entry in slice)
        {
            yield return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Updates the value for the specified key using the provided function and stores the result.
    /// </summary>
    /// <param name="key">The key to update.</param>
    /// <param name="updateFunc">The function to apply to the current value.</param>
    /// <returns>The new value stored, or default if the key was not found.</returns>
    public TValue? Update(TKey key, Func<TValue, TValue> updateFunc)
    {
        var tree = _tree;
        var index = FindKeyIndex(tree, key);
        if (index >= 0)
        {
            var entry = tree[index];
            var newValue = updateFunc(entry.Value);
            if (!entry.Value.Equals(newValue))
            {
                _tree = tree.RemoveAt(index).Insert(index, new MapEntry<TKey, TValue>(entry.Key, newValue));
            }

            return newValue;
        }

        return default;
    }

    /// <summary>
    /// Retains only the entries that satisfy the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate to test each entry.</param>
    public void Retain(Func<TKey, TValue, bool> predicate)
    {
        var newEntries = new List<MapEntry<TKey, TValue>>();

        foreach (var entry in _tree)
        {
            if (predicate(entry.Key, entry.Value))
            {
                newEntries.Add(entry);
            }
        }

        _tree = Build(newEntries);
    }

    /// <summary>
    /// Gets the first entry in the map.
    /// </summary>
    /// <returns>The first entry, or an empty Maybe if the map is empty.</returns>
    public Maybe<KeyValuePair<TKey, TValue>> First()
    {
        if (_tree.IsEmpty)
        {
            return Maybe<KeyValuePair<TKey, TValue>>.None;
        }

        var entry = _tree[0];
        return new Maybe<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(entry.Key, entry.Value));
    }

    /// <summary>
    /// Gets the last entry in the map.
    /// </summary>
    /// <returns>The last entry, or an empty Maybe if the map is empty.</returns>
    public Maybe<KeyValuePair<TKey, TValue>> Last()
    {
        if (_tree.IsEmpty)
        {
            return Maybe<KeyValuePair<TKey, TValue>>.None;
        }

        var entry = _tree[_tree.Length - 1];
        return new Maybe<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(entry.Key, entry.Value));
    }

    /// <summary>
    /// Merges another TreeMap into this one.
    /// </summary>
    /// <param name="other">The TreeMap to merge.</param>
    public void InsertTree(TreeMap<TKey, TValue> other)
    {
        Extend(other);
    }

    /// <summary>
    /// Gets all keys in the map.
    /// </summary>
    /// <returns>An enumerable of all keys.</returns>
    public IEnumerable<TKey> Keys => this.Select(kvp => kvp.Key);

    /// <summary>
    /// Gets all values in the map.
    /// </summary>
    /// <returns>An enumerable of all values.</returns>
    public IEnumerable<TValue> Values => this.Select(kvp => kvp.Value);

    /// <summary>
    /// Returns an enumerator that iterates through the map.
    /// </summary>
    /// <returns>An enumerator for the map.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _tree.Select(entry => new KeyValuePair<TKey, TValue>(entry.Key, entry.Value)).GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the map.
    /// </summary>
    /// <returns>An enumerator for the map.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    SumTree<MapEntry<TKey, TValue>, MapKey<TKey>, MapKeySummaryOperations<TKey, TValue>> BuildFromUnordered(
        IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
        // OrderBy is stable, so on key conflicts the later pair (the incoming value) wins,
        // matching repeated Insert semantics.
        var sorted = pairs.OrderBy(pair => pair.Key).ToList();
        var entries = new List<MapEntry<TKey, TValue>>(sorted.Count);

        for (var i = 0; i < sorted.Count; i++)
        {
            if (i + 1 < sorted.Count && sorted[i].Key.Equals(sorted[i + 1].Key))
            {
                continue;
            }

            entries.Add(new MapEntry<TKey, TValue>(sorted[i].Key, sorted[i].Value));
        }

        return Build(entries);
    }

    static SumTree<MapEntry<TKey, TValue>, MapKey<TKey>, MapKeySummaryOperations<TKey, TValue>> Build(
        List<MapEntry<TKey, TValue>> entries) =>
        new(entries.ToArray());

    static long FindKeyIndex(
        SumTree<MapEntry<TKey, TValue>, MapKey<TKey>, MapKeySummaryOperations<TKey, TValue>> tree, TKey key)
    {
        var index = FindInsertionIndex(tree, key);
        if (index < tree.Length && tree[index].Key.Equals(key))
        {
            return index;
        }

        return -1;
    }

    static long FindInsertionIndex(
        SumTree<MapEntry<TKey, TValue>, MapKey<TKey>, MapKeySummaryOperations<TKey, TValue>> tree, TKey key)
    {
        // The node summary stores the greatest key in the range, so the first position whose
        // prefix maximum reaches the target is exactly the lower bound of the key.
        var index = tree.FindPosition(new KeySeek(key));
        return index < 0 ? tree.Length : index;
    }

    readonly struct KeySeek(TKey key) : ISummarySeek<MapKey<TKey>>
    {
        public bool IsReached(in MapKey<TKey> summary) =>
            !summary.IsEmpty && summary.Key!.CompareTo(key) >= 0;
    }
}
