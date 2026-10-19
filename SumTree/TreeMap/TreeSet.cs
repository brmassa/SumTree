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
    TreeMap<T, Unit> _map;

    /// <summary>
    /// Initializes a new empty TreeSet.
    /// </summary>
    public TreeSet()
    {
        _map = new TreeMap<T, Unit>();
    }

    /// <summary>
    /// Initializes a new TreeSet from a sequence of elements.
    /// Duplicates are dropped; the input does not need to be sorted.
    /// </summary>
    /// <param name="orderedEntries">The elements to initialize with.</param>
    public TreeSet(IEnumerable<T> orderedEntries)
    {
        ArgumentNullException.ThrowIfNull(orderedEntries);
        _map = CreateMap(orderedEntries);
    }

    TreeSet(TreeMap<T, Unit> map)
    {
        _map = map;
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
        return _map.Insert(item, default);
    }

    /// <summary>
    /// Removes an element from the set.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>True if the element was removed, false if it didn't exist.</returns>
    public bool Remove(T item)
    {
        return _map.TryRemove(item, out _);
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
        ArgumentNullException.ThrowIfNull(items);

        var materialized = items as ICollection<T> ?? [.. items];
        if (materialized.Count == 0)
        {
            return;
        }

        if (_map.IsEmpty)
        {
            _map = CreateMap(materialized);
            return;
        }

        if (materialized.Count > _map.Count)
        {
            // Rebuilding once is cheaper than inserting a large batch element by element.
            _map = CreateMap(_map.Keys.Concat(materialized));
            return;
        }

        foreach (var item in materialized)
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
    /// <returns>The first element, or null if the set is empty.</returns>
    public Maybe<T> First()
    {
        if (_map.IsEmpty)
        {
            return Maybe<T>.None;
        }

        return new Maybe<T>(_map.First().Value.Key);
    }

    /// <summary>
    /// Gets the last element in the set.
    /// </summary>
    /// <returns>The last element, or null if the set is empty.</returns>
    public Maybe<T> Last()
    {
        if (_map.IsEmpty)
        {
            return Maybe<T>.None;
        }

        return new Maybe<T>(_map.Last().Value.Key);
    }


    /// <summary>
    /// Finds the element with the greatest value less than or equal to the specified element.
    /// </summary>
    /// <param name="item">The element to search for.</param>
    /// <returns>The closest element, or default if not found.</returns>
    public Maybe<T> Closest(T item)
    {
        var closest = _map.Closest(item);
        return closest.HasValue ? new Maybe<T>(closest.Value.Key) : Maybe<T>.None;
    }

    /// <summary>
    /// Creates a new set containing elements that are in this set but not in the other set.
    /// </summary>
    /// <param name="other">The other set to subtract.</param>
    /// <returns>A new set containing the difference.</returns>
    public TreeSet<T> Except(TreeSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var collected = new List<T>();
        using var left = GetEnumerator();
        using var right = other.GetEnumerator();
        var hasLeft = left.MoveNext();
        var hasRight = right.MoveNext();

        while (hasLeft)
        {
            if (!hasRight)
            {
                collected.Add(left.Current);
                hasLeft = left.MoveNext();
                continue;
            }

            var comparison = left.Current.CompareTo(right.Current);
            if (comparison < 0)
            {
                collected.Add(left.Current);
                hasLeft = left.MoveNext();
            }
            else if (comparison > 0)
            {
                hasRight = right.MoveNext();
            }
            else
            {
                hasLeft = left.MoveNext();
                hasRight = right.MoveNext();
            }
        }

        return FromSortedUnique(collected);
    }

    /// <summary>
    /// Creates a new set containing elements that are in both this set and the other set.
    /// </summary>
    /// <param name="other">The other set to intersect with.</param>
    /// <returns>A new set containing the intersection.</returns>
    public TreeSet<T> Intersect(TreeSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var collected = new List<T>();
        using var left = GetEnumerator();
        using var right = other.GetEnumerator();
        var hasLeft = left.MoveNext();
        var hasRight = right.MoveNext();

        while (hasLeft && hasRight)
        {
            var comparison = left.Current.CompareTo(right.Current);
            if (comparison < 0)
            {
                hasLeft = left.MoveNext();
            }
            else if (comparison > 0)
            {
                hasRight = right.MoveNext();
            }
            else
            {
                collected.Add(left.Current);
                hasLeft = left.MoveNext();
                hasRight = right.MoveNext();
            }
        }

        return FromSortedUnique(collected);
    }

    /// <summary>
    /// Creates a new set containing elements that are in either this set or the other set.
    /// </summary>
    /// <param name="other">The other set to union with.</param>
    /// <returns>A new set containing the union.</returns>
    public TreeSet<T> Union(TreeSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var collected = new List<T>();
        using var left = GetEnumerator();
        using var right = other.GetEnumerator();
        var hasLeft = left.MoveNext();
        var hasRight = right.MoveNext();

        while (hasLeft && hasRight)
        {
            var comparison = left.Current.CompareTo(right.Current);
            if (comparison < 0)
            {
                collected.Add(left.Current);
                hasLeft = left.MoveNext();
            }
            else if (comparison > 0)
            {
                collected.Add(right.Current);
                hasRight = right.MoveNext();
            }
            else
            {
                collected.Add(left.Current);
                hasLeft = left.MoveNext();
                hasRight = right.MoveNext();
            }
        }

        while (hasLeft)
        {
            collected.Add(left.Current);
            hasLeft = left.MoveNext();
        }

        while (hasRight)
        {
            collected.Add(right.Current);
            hasRight = right.MoveNext();
        }

        return FromSortedUnique(collected);
    }

    /// <summary>
    /// Creates a new set containing elements that are in either this set or the other set, but not both.
    /// </summary>
    /// <param name="other">The other set to symmetric difference with.</param>
    /// <returns>A new set containing the symmetric difference.</returns>
    public TreeSet<T> SymmetricExcept(TreeSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var collected = new List<T>();
        using var left = GetEnumerator();
        using var right = other.GetEnumerator();
        var hasLeft = left.MoveNext();
        var hasRight = right.MoveNext();

        while (hasLeft && hasRight)
        {
            var comparison = left.Current.CompareTo(right.Current);
            if (comparison < 0)
            {
                collected.Add(left.Current);
                hasLeft = left.MoveNext();
            }
            else if (comparison > 0)
            {
                collected.Add(right.Current);
                hasRight = right.MoveNext();
            }
            else
            {
                hasLeft = left.MoveNext();
                hasRight = right.MoveNext();
            }
        }

        while (hasLeft)
        {
            collected.Add(left.Current);
            hasLeft = left.MoveNext();
        }

        while (hasRight)
        {
            collected.Add(right.Current);
            hasRight = right.MoveNext();
        }

        return FromSortedUnique(collected);
    }

    /// <summary>
    /// Determines whether this set is a subset of the specified set.
    /// </summary>
    /// <param name="other">The other set to check.</param>
    /// <returns>True if this set is a subset of the other set, false otherwise.</returns>
    public bool IsSubsetOf(TreeSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        using var left = GetEnumerator();
        using var right = other.GetEnumerator();
        var hasRight = right.MoveNext();

        while (left.MoveNext())
        {
            var item = left.Current;
            while (hasRight && right.Current.CompareTo(item) < 0)
            {
                hasRight = right.MoveNext();
            }

            if (!hasRight || right.Current.CompareTo(item) != 0)
            {
                return false;
            }

            hasRight = right.MoveNext();
        }

        return true;
    }

    /// <summary>
    /// Determines whether this set is a superset of the specified set.
    /// </summary>
    /// <param name="other">The other set to check.</param>
    /// <returns>True if this set is a superset of the other set, false otherwise.</returns>
    public bool IsSupersetOf(TreeSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.IsSubsetOf(this);
    }

    /// <summary>
    /// Determines whether this set overlaps with the specified set.
    /// </summary>
    /// <param name="other">The other set to check.</param>
    /// <returns>True if the sets overlap, false otherwise.</returns>
    public bool Overlaps(TreeSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        using var left = GetEnumerator();
        using var right = other.GetEnumerator();
        var hasLeft = left.MoveNext();
        var hasRight = right.MoveNext();

        while (hasLeft && hasRight)
        {
            var comparison = left.Current.CompareTo(right.Current);
            if (comparison < 0)
            {
                hasLeft = left.MoveNext();
            }
            else if (comparison > 0)
            {
                hasRight = right.MoveNext();
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether this set and the specified set contain the same elements.
    /// </summary>
    /// <param name="other">The other set to check.</param>
    /// <returns>True if the sets are equal, false otherwise.</returns>
    public bool SetEquals(TreeSet<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Count == other.Count && IsSubsetOf(other);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    /// <returns>An enumerator for the set.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return _map.Keys.GetEnumerator();
    }

    static TreeMap<T, Unit> CreateMap(IEnumerable<T> items)
    {
        var list = new List<T>(items);

        var sorted = true;
        for (var i = 1; i < list.Count; i++)
        {
            if (list[i - 1].CompareTo(list[i]) > 0)
            {
                sorted = false;
                break;
            }
        }

        if (!sorted)
        {
            list.Sort();
        }

        var pairs = new List<KeyValuePair<T, Unit>>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0 && list[i - 1].Equals(list[i]))
            {
                continue;
            }

            pairs.Add(new KeyValuePair<T, Unit>(list[i], default));
        }

        return new TreeMap<T, Unit>(pairs);
    }

    static TreeSet<T> FromSortedUnique(List<T> items)
    {
        var pairs = new List<KeyValuePair<T, Unit>>(items.Count);
        foreach (var item in items)
        {
            pairs.Add(new KeyValuePair<T, Unit>(item, default));
        }

        return new TreeSet<T>(new TreeMap<T, Unit>(pairs));
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
