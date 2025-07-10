using System.Diagnostics.CodeAnalysis;

namespace SumTree.TreeMap;

/// <summary>
/// Represents a key-value entry in a TreeMap.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
public readonly struct MapEntry<TKey, TValue> : IEquatable<MapEntry<TKey, TValue>>
    where TKey : IComparable<TKey>, IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    /// <summary>
    /// Gets the key of the entry.
    /// </summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the value of the entry.
    /// </summary>
    public TValue Value { get; }

    /// <summary>
    /// Initializes a new MapEntry with the specified key and value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public MapEntry(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Determines whether this entry is equal to another entry.
    /// </summary>
    /// <param name="other">The other entry to compare with.</param>
    /// <returns>True if the entries are equal, false otherwise.</returns>
    public bool Equals(MapEntry<TKey, TValue> other)
    {
        return Key.Equals(other.Key) && Value.Equals(other.Value);
    }

    /// <summary>
    /// Determines whether this entry is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the objects are equal, false otherwise.</returns>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is MapEntry<TKey, TValue> other && Equals(other);
    }

    /// <summary>
    /// Returns the hash code for this entry.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Value);
    }

    /// <summary>
    /// Returns a string representation of this entry.
    /// </summary>
    /// <returns>A string representation.</returns>
    public override string ToString()
    {
        return $"[{Key}: {Value}]";
    }

    /// <summary>
    /// Determines whether two entries are equal.
    /// </summary>
    /// <param name="left">The left entry.</param>
    /// <param name="right">The right entry.</param>
    /// <returns>True if the entries are equal, false otherwise.</returns>
    public static bool operator ==(MapEntry<TKey, TValue> left, MapEntry<TKey, TValue> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two entries are not equal.
    /// </summary>
    /// <param name="left">The left entry.</param>
    /// <param name="right">The right entry.</param>
    /// <returns>True if the entries are not equal, false otherwise.</returns>
    public static bool operator !=(MapEntry<TKey, TValue> left, MapEntry<TKey, TValue> right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Represents a key for use in TreeMap operations and seeking.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
public readonly struct MapKey<TKey> : IEquatable<MapKey<TKey>>, IComparable<MapKey<TKey>>
    where TKey : IComparable<TKey>, IEquatable<TKey>
{
    /// <summary>
    /// Gets the key value. May be null for empty/default keys.
    /// </summary>
    public TKey? Key { get; }

    /// <summary>
    /// Gets whether this is an empty key.
    /// </summary>
    public bool IsEmpty => Key == null;

    /// <summary>
    /// Initializes a new MapKey with the specified key value.
    /// </summary>
    /// <param name="key">The key value.</param>
    public MapKey(TKey? key)
    {
        Key = key;
    }

    /// <summary>
    /// Gets the default (empty) MapKey.
    /// </summary>
    public static MapKey<TKey> Empty => new(default(TKey));

    /// <summary>
    /// Determines whether this key is equal to another key.
    /// </summary>
    /// <param name="other">The other key to compare with.</param>
    /// <returns>True if the keys are equal, false otherwise.</returns>
    public bool Equals(MapKey<TKey> other)
    {
        if (Key == null && other.Key == null) return true;
        if (Key == null || other.Key == null) return false;
        return Key.Equals(other.Key);
    }

    /// <summary>
    /// Determines whether this key is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the objects are equal, false otherwise.</returns>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is MapKey<TKey> other && Equals(other);
    }

    /// <summary>
    /// Returns the hash code for this key.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return Key?.GetHashCode() ?? 0;
    }

    /// <summary>
    /// Compares this key with another key.
    /// </summary>
    /// <param name="other">The other key to compare with.</param>
    /// <returns>A value indicating the relative order of the keys.</returns>
    public int CompareTo(MapKey<TKey> other)
    {
        if (Key == null && other.Key == null) return 0;
        if (Key == null) return -1;
        if (other.Key == null) return 1;
        return Key.CompareTo(other.Key);
    }

    /// <summary>
    /// Returns a string representation of this key.
    /// </summary>
    /// <returns>A string representation.</returns>
    public override string ToString()
    {
        return Key?.ToString() ?? "<empty>";
    }

    /// <summary>
    /// Determines whether two keys are equal.
    /// </summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>True if the keys are equal, false otherwise.</returns>
    public static bool operator ==(MapKey<TKey> left, MapKey<TKey> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two keys are not equal.
    /// </summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>True if the keys are not equal, false otherwise.</returns>
    public static bool operator !=(MapKey<TKey> left, MapKey<TKey> right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Determines whether the left key is less than the right key.
    /// </summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>True if the left key is less than the right key, false otherwise.</returns>
    public static bool operator <(MapKey<TKey> left, MapKey<TKey> right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Determines whether the left key is less than or equal to the right key.
    /// </summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>True if the left key is less than or equal to the right key, false otherwise.</returns>
    public static bool operator <=(MapKey<TKey> left, MapKey<TKey> right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Determines whether the left key is greater than the right key.
    /// </summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>True if the left key is greater than the right key, false otherwise.</returns>
    public static bool operator >(MapKey<TKey> left, MapKey<TKey> right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Determines whether the left key is greater than or equal to the right key.
    /// </summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>True if the left key is greater than or equal to the right key, false otherwise.</returns>
    public static bool operator >=(MapKey<TKey> left, MapKey<TKey> right)
    {
        return left.CompareTo(right) >= 0;
    }
}
