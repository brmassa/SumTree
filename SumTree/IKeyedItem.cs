namespace SumTree;

/// <summary>
/// Represents an item that can be uniquely identified by a key.
/// This interface enables efficient lookups and range operations in sorted data structures.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IKeyedItem<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Gets the key that uniquely identifies this item.
    /// </summary>
    TKey Key { get; }
}

/// <summary>
/// Represents an item that can provide a summary of itself.
/// This interface enables efficient aggregation and querying operations.
/// </summary>
/// <typeparam name="TSummary">The type of the summary.</typeparam>
public interface IItem<TSummary> where TSummary : IEquatable<TSummary>
{
    /// <summary>
    /// Gets the summary of this item.
    /// </summary>
    /// <param name="context">The context for computing the summary.</param>
    /// <returns>The summary of this item.</returns>
    TSummary Summary(object? context = null);
}

/// <summary>
/// Represents an item that is both keyed and provides a summary.
/// This combines the functionality of IKeyedItem and IItem.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TSummary">The type of the summary.</typeparam>
public interface IKeyedSummaryItem<TKey, TSummary> : IKeyedItem<TKey>, IItem<TSummary>
    where TKey : IEquatable<TKey>
    where TSummary : IEquatable<TSummary>
{
}

/// <summary>
/// Represents a summary that can be combined with other summaries.
/// This interface enables efficient aggregation of summary information.
/// </summary>
/// <typeparam name="TSummary">The type of the summary.</typeparam>
public interface ISummary<TSummary> where TSummary : IEquatable<TSummary>
{
    /// <summary>
    /// Gets the zero/identity summary value.
    /// </summary>
    /// <param name="context">The context for computing the zero value.</param>
    /// <returns>The zero/identity summary value.</returns>
    TSummary Zero(object? context = null);

    /// <summary>
    /// Adds another summary to this summary.
    /// </summary>
    /// <param name="left">The left summary to combine.</param>
    /// <param name="right">The right summary to combine.</param>
    /// <param name="context">The context for the addition operation.</param>
    /// <returns>The combined summary.</returns>
    TSummary Add(TSummary left, TSummary right, object? context = null);
}

/// <summary>
/// Represents a seek target for cursor navigation.
/// This interface enables efficient seeking to specific positions in a tree.
/// </summary>
/// <typeparam name="TDimension">The dimension type for seeking.</typeparam>
public interface ISeekTarget<TDimension> where TDimension : IEquatable<TDimension>
{
    /// <summary>
    /// Compares this seek target with a cursor location.
    /// </summary>
    /// <param name="cursorLocation">The current cursor location.</param>
    /// <param name="context">The context for the comparison.</param>
    /// <returns>A value indicating the relative order.</returns>
    int Compare(TDimension cursorLocation, object? context = null);
}

/// <summary>
/// Extension methods for working with keyed items.
/// </summary>
public static class KeyedItemExtensions
{
    /// <summary>
    /// Extracts keys from a collection of keyed items.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="items">The collection of keyed items.</param>
    /// <returns>An enumerable of keys.</returns>
    public static IEnumerable<TKey> GetKeys<TItem, TKey>(this IEnumerable<TItem> items)
        where TItem : IKeyedItem<TKey>
        where TKey : IEquatable<TKey>
    {
        return items.Select(item => item.Key);
    }

    /// <summary>
    /// Groups keyed items by their keys.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="items">The collection of keyed items.</param>
    /// <returns>A dictionary grouped by keys.</returns>
    public static Dictionary<TKey, List<TItem>> GroupByKey<TItem, TKey>(this IEnumerable<TItem> items)
        where TItem : IKeyedItem<TKey>
        where TKey : IEquatable<TKey>
    {
        return items.GroupBy(item => item.Key).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Finds an item by its key.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="items">The collection of keyed items.</param>
    /// <param name="key">The key to search for.</param>
    /// <returns>The item with the specified key, or default if not found.</returns>
    public static TItem? FindByKey<TItem, TKey>(this IEnumerable<TItem> items, TKey key)
        where TItem : IKeyedItem<TKey>
        where TKey : IEquatable<TKey>
    {
        return items.FirstOrDefault(item => item.Key.Equals(key));
    }

    /// <summary>
    /// Checks if a collection contains an item with the specified key.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="items">The collection of keyed items.</param>
    /// <param name="key">The key to search for.</param>
    /// <returns>True if an item with the key exists, false otherwise.</returns>
    public static bool ContainsKey<TItem, TKey>(this IEnumerable<TItem> items, TKey key)
        where TItem : IKeyedItem<TKey>
        where TKey : IEquatable<TKey>
    {
        return items.Any(item => item.Key.Equals(key));
    }
}

/// <summary>
/// Extension methods for working with summary items.
/// </summary>
public static class SummaryItemExtensions
{
    /// <summary>
    /// Computes summaries for a collection of items.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <typeparam name="TSummary">The type of the summary.</typeparam>
    /// <param name="items">The collection of items.</param>
    /// <param name="context">The context for computing summaries.</param>
    /// <returns>An enumerable of summaries.</returns>
    public static IEnumerable<TSummary> GetSummaries<TItem, TSummary>(this IEnumerable<TItem> items, object? context = null)
        where TItem : IItem<TSummary>
        where TSummary : IEquatable<TSummary>
    {
        return items.Select(item => item.Summary(context));
    }

    /// <summary>
    /// Computes the total summary for a collection of items.
    /// </summary>
    /// <typeparam name="TItem">The type of the item.</typeparam>
    /// <typeparam name="TSummary">The type of the summary.</typeparam>
    /// <param name="items">The collection of items.</param>
    /// <param name="summaryProvider">The summary provider for combining summaries.</param>
    /// <param name="context">The context for computing summaries.</param>
    /// <returns>The total summary.</returns>
    public static TSummary TotalSummary<TItem, TSummary>(
        this IEnumerable<TItem> items,
        ISummary<TSummary> summaryProvider,
        object? context = null)
        where TItem : IItem<TSummary>
        where TSummary : IEquatable<TSummary>
    {
        var result = summaryProvider.Zero(context);
        foreach (var item in items)
        {
            result = summaryProvider.Add(result, item.Summary(context));
        }
        return result;
    }
}

/// <summary>
/// A basic implementation of IKeyedItem for simple key-value pairs.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
public readonly struct KeyedItem<TKey, TValue> : IKeyedItem<TKey>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    /// <summary>
    /// Gets the key of this item.
    /// </summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the value of this item.
    /// </summary>
    public TValue Value { get; }

    /// <summary>
    /// Initializes a new keyed item.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public KeyedItem(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Determines whether this item is equal to another item.
    /// </summary>
    /// <param name="other">The other item to compare with.</param>
    /// <returns>True if the items are equal, false otherwise.</returns>
    public bool Equals(KeyedItem<TKey, TValue> other)
    {
        return Key.Equals(other.Key) && Value.Equals(other.Value);
    }

    /// <summary>
    /// Determines whether this item is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if the objects are equal, false otherwise.</returns>
    public override bool Equals(object? obj)
    {
        return obj is KeyedItem<TKey, TValue> other && Equals(other);
    }

    /// <summary>
    /// Returns the hash code for this item.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Value);
    }

    /// <summary>
    /// Returns a string representation of this item.
    /// </summary>
    /// <returns>A string representation.</returns>
    public override string ToString()
    {
        return $"[{Key}: {Value}]";
    }

    /// <summary>
    /// Implicitly converts a key-value pair to a keyed item.
    /// </summary>
    /// <param name="pair">The key-value pair to convert.</param>
    /// <returns>A new keyed item.</returns>
    public static implicit operator KeyedItem<TKey, TValue>(KeyValuePair<TKey, TValue> pair)
    {
        return new KeyedItem<TKey, TValue>(pair.Key, pair.Value);
    }

    /// <summary>
    /// Implicitly converts a keyed item to a key-value pair.
    /// </summary>
    /// <param name="item">The keyed item to convert.</param>
    /// <returns>A new key-value pair.</returns>
    public static implicit operator KeyValuePair<TKey, TValue>(KeyedItem<TKey, TValue> item)
    {
        return new KeyValuePair<TKey, TValue>(item.Key, item.Value);
    }
}
