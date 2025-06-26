namespace SumTree;

/// <summary>
/// Defines a summary dimension that can be computed over a sequence of elements.
/// A summary dimension tracks a specific aspect of the data (e.g., line numbers, indentation, bracket counts).
/// </summary>
/// <typeparam name="T">The type of elements in the sequence.</typeparam>
/// <typeparam name="TSummary">The type of the summary value.</typeparam>
public interface ISummaryDimension<T, TSummary>
    where T : IEquatable<T>
    where TSummary : IEquatable<TSummary>
{
    /// <summary>
    /// Gets the identity/empty summary value.
    /// This is returned for empty sequences.
    /// </summary>
    TSummary Identity { get; }

    /// <summary>
    /// Computes a summary for a single element.
    /// </summary>
    /// <param name="element">The element to summarize.</param>
    /// <returns>The summary for the single element.</returns>
    TSummary SummarizeElement(T element);

    /// <summary>
    /// Computes a summary for a span of elements.
    /// </summary>
    /// <param name="elements">The span of elements to summarize.</param>
    /// <returns>The summary for the span of elements.</returns>
    TSummary SummarizeSpan(ReadOnlySpan<T> elements);

    /// <summary>
    /// Combines two summary values into one.
    /// This operation must be associative: Combine(a, Combine(b, c)) == Combine(Combine(a, b), c)
    /// </summary>
    /// <param name="left">The left summary value.</param>
    /// <param name="right">The right summary value.</param>
    /// <returns>The combined summary value.</returns>
    TSummary Combine(TSummary left, TSummary right);

    /// <summary>
    /// Determines if the given summary can be extended (is seeking more elements).
    /// This is used for cursor navigation and range queries.
    /// </summary>
    /// <param name="summary">The summary to check.</param>
    /// <returns>True if the summary can be extended, false otherwise.</returns>
    bool CanExtend(TSummary summary);

    /// <summary>
    /// Compares two summary values for ordering.
    /// Used for binary search operations and cursor positioning.
    /// </summary>
    /// <param name="left">The left summary value.</param>
    /// <param name="right">The right summary value.</param>
    /// <returns>A value indicating the relative order of the summaries.</returns>
    int Compare(TSummary left, TSummary right);
}