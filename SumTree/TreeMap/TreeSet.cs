using System.Collections;

namespace SumTree.TreeMap;

/// <summary>
/// A unit type used as a marker value in TreeSet.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// Determines whether this unit is equal to another unit.
    /// All units are equal.
    /// </summary>
    /// <param name="other">The other unit.</param>
    /// <returns>Always true.</returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines whether this unit is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the object is a Unit, false otherwise.</returns>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Returns the hash code for this unit.
    /// </summary>
    /// <returns>Always zero.</returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns a string representation of this unit.
    /// </summary>
    /// <returns>The string "()".</returns>
    public override string ToString() => "()";

    /// <summary>
    /// Determines whether two units are equal.
    /// </summary>
    /// <param name="left">The left unit.</param>
    /// <param name="right">The right unit.</param>
    /// <returns>Always true.</returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Determines whether two units are not equal.
    /// </summary>
    /// <param name="left">The left unit.</param>
    /// <param name="right">The right unit.</param>
    /// <returns>Always false.</returns>
    public static bool operator !=(Unit left, Unit right) => false;
}

/// <summary>
/// A TreeSet is a sorted set data structure built on top of TreeMap.
/// It provides O(log n) insertion, deletion, and lookup operations.
/// </summary>
/// <typeparam name="T">The type of elements in the set.</typeparam>
public class TreeSet<T> : IEnumerable<T>
    where T : IComparable<T>, IEquatable<T>
{
    private readonly TreeMap<T, Unit> _map;
    private static readonly Unit Marker = new();

    /// <summary>
    /// Initializes a new empty TreeSet.
    /// </summary>
    public TreeSet()
    {
        _map = new TreeMap<T, Unit>();
    }

    /// <summary>
    /// Initializes a new TreeSet from ordered entries.
    /// </summary>
    /// <param name="orderedEntries">The ordered entries to initialize with.</param>
    public TreeSet(IEnumerable<T> orderedEntries)
    {
        _map = new TreeMap<T, Unit>();
        foreach (var entry in orderedEntries)
        {
            _map.Insert(entry, Marker);
        }
    }

    /// <summary>
    /// Gets whether the set is empty.
    /// </summary>
    public bool IsEmpty => _map.IsEmpty;

    /// <summary>
    /// Gets the number of elements in the set.
    /// </summary>
    public int Count => _map.Count;

    /// <summary>
    /// Adds an element to the set.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>True if the element was added, false if it already existed.</returns>
    public bool Add(T item)
    {
        bool existed = _map.ContainsKey(item);
        _map.Insert(item, Marker);
        return !existed;
    }

    /// <summary>
    /// Removes an element from the set.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>True if the element was removed, false if it didn't exist.</returns>
    public bool Remove(T item)
    {
        if (_map.ContainsKey(item))
        {
            _map.Remove(item);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>True if the set contains the element, false otherwise.</returns>
    public bool Contains(T item)
    {
        return _map.ContainsKey(item);
    }

    /// <summary>
    /// Removes all elements from the set.
    /// </summary>
    public void Clear()
    {
        _map.Clear();
    }

    /// <summary>
    /// Adds all elements from the specified collection to the set.
    /// </summary>
    /// <param name="items">The elements to add.</param>
    public void Extend(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Returns an iterator starting from the specified element.
    /// </summary>
    /// <param name="fromItem">The element to start iteration from.</param>
    /// <returns>An enumerator that starts from the specified element.</returns>
    public IEnumerable<T> IterateFrom(T fromItem)
    {
        return _map.IterateFrom(fromItem).Select(kvp => kvp.Key);
    }

    /// <summary>
    /// Gets the first element in the set.
    /// </summary>
    /// <returns>The first element, or default if the set is empty.</returns>
    public T? First()
    {
        var first = _map.First();
        return first.HasValue ? first.Value.Key : default;
    }

    /// <summary>
    /// Gets the last element in the set.
    /// </summary>
    /// <returns>The last element, or default if the set is empty.</returns>
    public T? Last()
    {
        var last = _map.Last();
        return last.HasValue ? last.Value.Key : default;
    }

    /// <summary>
    /// Finds the element with the greatest value less than or equal to the specified element.
    /// </summary>
    /// <param name="item">The element to search for.</param>
    /// <returns>The closest element, or default if not found.</returns>
    public T? Closest(T item)
    {
        var closest = _map.Closest(item);
        return closest.HasValue ? closest.Value.Key : default;
    }

    /// <summary>
    /// Creates a new set containing elements that are in this set but not in the other set.
    /// </summary>
    /// <param name="other">The other set to subtract.</param>
    /// <returns>A new set containing the difference.</returns>
    public TreeSet<T> Except(TreeSet<T> other)
    {
        var result = new TreeSet<T>();
        foreach (var item in this)
        {
            if (!other.Contains(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a new set containing elements that are in both this set and the other set.
    /// </summary>
    /// <param name="other">The other set to intersect with.</param>
    /// <returns>A new set containing the intersection.</returns>
    public TreeSet<T> Intersect(TreeSet<T> other)
    {
        var result = new TreeSet<T>();
        foreach (var item in this)
        {
            if (other.Contains(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a new set containing elements that are in either this set or the other set.
    /// </summary>
    /// <param name="other">The other set to union with.</param>
    /// <returns>A new set containing the union.</returns>
    public TreeSet<T> Union(TreeSet<T> other)
    {
        var result = new TreeSet<T>();
        foreach (var item in this)
        {
            result.Add(item);
        }
        foreach (var item in other)
        {
            result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// Creates a new set containing elements that are in either this set or the other set, but not both.
    /// </summary>
    /// <param name="other">The other set to symmetric difference with.</param>
    /// <returns>A new set containing the symmetric difference.</returns>
    public TreeSet<T> SymmetricExcept(TreeSet<T> other)
    {
        var result = new TreeSet<T>();
        foreach (var item in this)
        {
            if (!other.Contains(item))
            {
                result.Add(item);
            }
        }
        foreach (var item in other)
        {
            if (!this.Contains(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>
    /// Determines whether this set is a subset of the specified set.
    /// </summary>
    /// <param name="other">The other set to check.</param>
    /// <returns>True if this set is a subset of the other set, false otherwise.</returns>
    public bool IsSubsetOf(TreeSet<T> other)
    {
        return this.All(item => other.Contains(item));
    }

    /// <summary>
    /// Determines whether this set is a superset of the specified set.
    /// </summary>
    /// <param name="other">The other set to check.</param>
    /// <returns>True if this set is a superset of the other set, false otherwise.</returns>
    public bool IsSupersetOf(TreeSet<T> other)
    {
        return other.All(item => this.Contains(item));
    }

    /// <summary>
    /// Determines whether this set overlaps with the specified set.
    /// </summary>
    /// <param name="other">The other set to check.</param>
    /// <returns>True if the sets overlap, false otherwise.</returns>
    public bool Overlaps(TreeSet<T> other)
    {
        return this.Any(item => other.Contains(item));
    }

    /// <summary>
    /// Determines whether this set and the specified set contain the same elements.
    /// </summary>
    /// <param name="other">The other set to check.</param>
    /// <returns>True if the sets are equal, false otherwise.</returns>
    public bool SetEquals(TreeSet<T> other)
    {
        return this.Count == other.Count && this.All(item => other.Contains(item));
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    /// <returns>An enumerator for the set.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return _map.Keys.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    /// <returns>An enumerator for the set.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
