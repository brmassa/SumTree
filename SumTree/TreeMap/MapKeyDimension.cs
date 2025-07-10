namespace SumTree.TreeMap;

/// <summary>
/// A summary dimension for MapKey that enables seeking by key in a TreeMap.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
public class MapKeyDimension<TKey, TValue> : ISummaryDimension<MapEntry<TKey, TValue>, MapKey<TKey>>
    where TKey : IComparable<TKey>, IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    /// <summary>
    /// Gets the identity/empty summary value.
    /// </summary>
    public MapKey<TKey> Identity => MapKey<TKey>.Empty;

    /// <summary>
    /// Computes a summary for a single map entry.
    /// </summary>
    /// <param name="element">The map entry to summarize.</param>
    /// <returns>The key of the entry wrapped in a MapKey.</returns>
    public MapKey<TKey> SummarizeElement(MapEntry<TKey, TValue> element)
    {
        return new MapKey<TKey>(element.Key);
    }

    /// <summary>
    /// Computes a summary for a span of map entries.
    /// For map entries, this returns the key of the last entry in the span.
    /// </summary>
    /// <param name="elements">The span of map entries to summarize.</param>
    /// <returns>The key of the last entry in the span.</returns>
    public MapKey<TKey> SummarizeSpan(ReadOnlySpan<MapEntry<TKey, TValue>> elements)
    {
        if (elements.IsEmpty)
            return Identity;

        // For a sorted map, we want the last (greatest) key in the span
        return new MapKey<TKey>(elements[elements.Length - 1].Key);
    }

    /// <summary>
    /// Combines two MapKey summaries.
    /// For map keys, this returns the greater of the two keys.
    /// </summary>
    /// <param name="left">The left summary.</param>
    /// <param name="right">The right summary.</param>
    /// <returns>The greater of the two keys.</returns>
    public MapKey<TKey> Combine(MapKey<TKey> left, MapKey<TKey> right)
    {
        // Return the greater key (rightmost in sorted order)
        return left.CompareTo(right) >= 0 ? left : right;
    }

    /// <summary>
    /// Determines if the given summary can be extended (is seeking more elements).
    /// For map keys, this is true if the key is not empty.
    /// </summary>
    /// <param name="summary">The summary to check.</param>
    /// <returns>True if the summary can be extended, false otherwise.</returns>
    public bool CanExtend(MapKey<TKey> summary)
    {
        return !summary.IsEmpty;
    }

    /// <summary>
    /// Compares two MapKey summaries for ordering.
    /// </summary>
    /// <param name="left">The left summary.</param>
    /// <param name="right">The right summary.</param>
    /// <returns>A value indicating the relative order of the summaries.</returns>
    public int Compare(MapKey<TKey> left, MapKey<TKey> right)
    {
        return left.CompareTo(right);
    }
}
