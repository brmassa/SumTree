using System.Collections;
using System.Diagnostics.CodeAnalysis;
using SumTree.Cursors;

namespace SumTree.TreeMap;

/// <summary>
/// A TreeMap is a sorted map data structure built on top of SumTree.
/// It provides O(log n) insertion, deletion, and lookup operations.
/// </summary>
/// <typeparam name="TKey">The type of keys in the map.</typeparam>
/// <typeparam name="TValue">The type of values in the map.</typeparam>
public class TreeMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : IComparable<TKey>, IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    private SumTree<MapEntry<TKey, TValue>> _tree;
    private static readonly MapKeyDimension<TKey, TValue> KeyDimension = new();

    /// <summary>
    /// Initializes a new empty TreeMap.
    /// </summary>
    public TreeMap()
    {
        _tree = SumTree<MapEntry<TKey, TValue>>.Empty.WithDimension(KeyDimension);
    }

    /// <summary>
    /// Initializes a new TreeMap from ordered entries.
    /// </summary>
    /// <param name="orderedEntries">The ordered entries to initialize with.</param>
    public TreeMap(IEnumerable<KeyValuePair<TKey, TValue>> orderedEntries)
    {
        var entries = orderedEntries.Select(kvp => new MapEntry<TKey, TValue>(kvp.Key, kvp.Value));
        _tree = entries.ToSumTree().WithDimension(KeyDimension);
    }

    /// <summary>
    /// Gets whether the map is empty.
    /// </summary>
    public bool IsEmpty => _tree.IsEmpty;

    /// <summary>
    /// Gets the number of entries in the map.
    /// </summary>
    public int Count => (int)_tree.Length;

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value if found, otherwise the default value.</returns>
    public TValue? this[TKey key]
    {
        get => Get(key);
        set => Insert(key, value!);
    }

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value if found, otherwise the default value.</returns>
    public TValue? Get(TKey key)
    {
        var index = FindKeyIndex(key);
        if (index >= 0)
        {
            var entry = _tree.ElementAt(index);
            return entry.Value;
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
        var index = FindKeyIndex(key);
        if (index >= 0)
        {
            var entry = _tree.ElementAt(index);
            value = entry.Value;
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
        return FindKeyIndex(key) >= 0;
    }

    /// <summary>
    /// Inserts or updates a key-value pair in the map.
    /// </summary>
    /// <param name="key">The key to insert or update.</param>
    /// <param name="value">The value to associate with the key.</param>
    public void Insert(TKey key, TValue value)
    {
        var entry = new MapEntry<TKey, TValue>(key, value);
        var index = FindInsertionIndex(key);

        // Check if key already exists
        if (index < _tree.Length)
        {
            var existingEntry = _tree.ElementAt(index);
            if (existingEntry.Key.Equals(key))
            {
                // Replace existing entry
                _tree = _tree.RemoveAt(index).Insert(index, entry);
                return;
            }
        }

        // Insert new entry
        _tree = _tree.Insert(index, entry);
    }

    /// <summary>
    /// Removes the entry with the specified key.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>The value that was removed, or default if the key was not found.</returns>
    public TValue? Remove(TKey key)
    {
        var index = FindKeyIndex(key);
        if (index >= 0)
        {
            var entry = _tree.ElementAt(index);
            _tree = _tree.RemoveAt(index);
            return entry.Value;
        }

        return default;
    }

    /// <summary>
    /// Removes all entries from the map.
    /// </summary>
    public void Clear()
    {
        _tree = SumTree<MapEntry<TKey, TValue>>.Empty.WithDimension(KeyDimension);
    }

    /// <summary>
    /// Extends the map with the specified key-value pairs.
    /// </summary>
    /// <param name="pairs">The key-value pairs to add.</param>
    public void Extend(IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
        foreach (var pair in pairs)
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
        var startIndex = FindInsertionIndex(startKey);
        var endIndex = FindInsertionIndex(endKey);

        if (startIndex < endIndex)
        {
            _tree = _tree.RemoveRange(startIndex, endIndex - startIndex);
        }
    }

    /// <summary>
    /// Finds the entry with the greatest key less than or equal to the specified key.
    /// </summary>
    /// <param name="key">The key to search for.</param>
    /// <returns>The closest entry, or null if not found.</returns>
    public KeyValuePair<TKey, TValue>? Closest(TKey key)
    {
        var index = FindInsertionIndex(key);
        if (index > 0)
        {
            var entry = _tree.ElementAt(index - 1);
            return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }

        return null;
    }

    /// <summary>
    /// Returns an iterator starting from the specified key.
    /// </summary>
    /// <param name="fromKey">The key to start iteration from.</param>
    /// <returns>An enumerator that starts from the specified key.</returns>
    public IEnumerable<KeyValuePair<TKey, TValue>> IterateFrom(TKey fromKey)
    {
        var startIndex = FindInsertionIndex(fromKey);

        for (long i = startIndex; i < _tree.Length; i++)
        {
            var entry = _tree.ElementAt(i);
            yield return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// Updates the value for the specified key using the provided function.
    /// </summary>
    /// <param name="key">The key to update.</param>
    /// <param name="updateFunc">The function to apply to the current value.</param>
    /// <returns>The result of the update function, or default if the key was not found.</returns>
    public TResult? Update<TResult>(TKey key, Func<TValue, TResult> updateFunc)
    {
        var index = FindKeyIndex(key);
        if (index >= 0)
        {
            var entry = _tree.ElementAt(index);
            var result = updateFunc(entry.Value);
            return result;
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

        _tree = newEntries.ToSumTree();
    }

    /// <summary>
    /// Gets the first entry in the map.
    /// </summary>
    /// <returns>The first entry, or null if the map is empty.</returns>
    public KeyValuePair<TKey, TValue>? First()
    {
        if (_tree.IsEmpty) return null;

        var entry = _tree.ElementAt(0);
        return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
    }

    /// <summary>
    /// Gets the last entry in the map.
    /// </summary>
    /// <returns>The last entry, or null if the map is empty.</returns>
    public KeyValuePair<TKey, TValue>? Last()
    {
        if (_tree.IsEmpty) return null;

        var entry = _tree.ElementAt(_tree.Length - 1);
        return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
    }

    /// <summary>
    /// Merges another TreeMap into this one.
    /// </summary>
    /// <param name="other">The TreeMap to merge.</param>
    public void InsertTree(TreeMap<TKey, TValue> other)
    {
        foreach (var pair in other)
        {
            Insert(pair.Key, pair.Value);
        }
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

    private long FindKeyIndex(TKey key)
    {
        for (long i = 0; i < _tree.Length; i++)
        {
            var entry = _tree.ElementAt(i);
            if (entry.Key.Equals(key))
            {
                return i;
            }
        }
        return -1;
    }

    private long FindInsertionIndex(TKey key)
    {
        long left = 0;
        long right = _tree.Length;

        while (left < right)
        {
            long mid = left + (right - left) / 2;
            var entry = _tree.ElementAt(mid);

            if (entry.Key.CompareTo(key) < 0)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        return left;
    }
}
