namespace SumTree.TreeMap;

/// <summary>
/// Typed summary operations for TreeMap entries: each tree node summarizes the greatest
/// key in its range, which enables O(log n) key seeks through the typed tree.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
public readonly struct MapKeySummaryOperations<TKey, TValue> :
    ISummaryOperations<MapEntry<TKey, TValue>, MapKey<TKey>>
    where TKey : IComparable<TKey>, IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    /// <summary>
    /// Gets the identity/empty summary value.
    /// </summary>
    public static MapKey<TKey> Identity => MapKey<TKey>.Empty;

    /// <summary>
    /// Computes the summary of a single map entry.
    /// </summary>
    /// <param name="element">The map entry to summarize.</param>
    /// <returns>The key of the entry.</returns>
    public static MapKey<TKey> SummarizeElement(MapEntry<TKey, TValue> element) => new(element.Key);

    /// <summary>
    /// Computes the summary of a span of map entries.
    /// The span must be sorted by key; the last (greatest) key wins.
    /// </summary>
    /// <param name="elements">The span of map entries to summarize.</param>
    /// <returns>The greatest key in the span.</returns>
    public static MapKey<TKey> SummarizeSpan(ReadOnlySpan<MapEntry<TKey, TValue>> elements) =>
        elements.IsEmpty ? Identity : new MapKey<TKey>(elements[elements.Length - 1].Key);

    /// <summary>
    /// Combines two summaries by taking the greater key.
    /// </summary>
    /// <param name="left">The left summary.</param>
    /// <param name="right">The right summary.</param>
    /// <returns>The greater of the two keys.</returns>
    public static MapKey<TKey> Combine(in MapKey<TKey> left, in MapKey<TKey> right) =>
        left.CompareTo(right) >= 0 ? left : right;
}
