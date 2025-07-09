namespace SumTree;

/// <summary>
/// Abstract base class for summary dimensions that provides common functionality.
/// </summary>
/// <typeparam name="T">The type of elements in the sequence.</typeparam>
/// <typeparam name="TSummary">The type of the summary value.</typeparam>
public abstract class SummaryDimensionBase<T, TSummary> : ISummaryDimension<T, TSummary>
    where T : IEquatable<T>
    where TSummary : IEquatable<TSummary>
{
    /// <inheritdoc/>
    public abstract TSummary Identity { get; }

    /// <inheritdoc/>
    public abstract TSummary SummarizeElement(T element);

    /// <inheritdoc/>
    public virtual TSummary SummarizeSpan(ReadOnlySpan<T> elements)
    {
        if (elements.IsEmpty)
            return Identity;

        var summary = this.SummarizeElement(elements[0]);
        for (var i = 1; i < elements.Length; i++)
        {
            summary = this.Combine(summary, this.SummarizeElement(elements[i]));
        }

        return summary;
    }

    /// <inheritdoc/>
    public abstract TSummary Combine(TSummary left, TSummary right);

    /// <inheritdoc/>
    public virtual bool CanExtend(TSummary summary)
    {
        return !summary.Equals(Identity);
    }

    /// <inheritdoc/>
    public virtual int Compare(TSummary left, TSummary right)
    {
        if (left is IComparable<TSummary> comparableLeft)
            return comparableLeft.CompareTo(right);

        if (left is IComparable comparableGeneric && right is IComparable)
            return comparableGeneric.CompareTo(right);

        throw new NotSupportedException(
            $"Summary type {typeof(TSummary).Name} does not implement IComparable<T> or IComparable");
    }

    /// <summary>
    /// Helper method to combine multiple summaries in sequence.
    /// </summary>
    /// <param name="summaries">The summaries to combine.</param>
    /// <returns>The combined summary.</returns>
    protected TSummary CombineMany(params TSummary[] summaries)
    {
        if (summaries.Length == 0)
            return Identity;

        var result = summaries[0];
        for (var i = 1; i < summaries.Length; i++)
        {
            result = this.Combine(result, summaries[i]);
        }

        return result;
    }

    /// <summary>
    /// Helper method to combine summaries from a span.
    /// </summary>
    /// <param name="summaries">The summaries to combine.</param>
    /// <returns>The combined summary.</returns>
    protected TSummary CombineMany(ReadOnlySpan<TSummary> summaries)
    {
        if (summaries.IsEmpty)
            return Identity;

        var result = summaries[0];
        for (var i = 1; i < summaries.Length; i++)
        {
            result = this.Combine(result, summaries[i]);
        }

        return result;
    }
}