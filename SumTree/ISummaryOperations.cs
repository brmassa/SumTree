namespace SumTree;

/// <summary>Compile-time operations for one immutable summary stored directly in each node.</summary>
/// <remarks>
/// Combine must be associative and Identity must be a two-sided identity. Span summaries
/// must equal the combination of their element summaries, including across chunk boundaries.
/// Operations must be pure and thread-safe; elements and summaries must remain immutable.
/// Choose a counter range and overflow policy that preserve these laws for supported inputs.
/// </remarks>
public interface ISummaryOperations<T, TSummary> where TSummary : struct
{
    abstract static TSummary Identity { get; }
    abstract static TSummary SummarizeElement(T element);
    abstract static TSummary SummarizeSpan(ReadOnlySpan<T> elements);
    abstract static TSummary Combine(in TSummary left, in TSummary right);
}

/// <summary>A monotonic seek target evaluated against accumulated prefix summaries.</summary>
/// <remarks>Once reached for a prefix, the target must remain reached for every longer prefix.</remarks>
public interface ISummarySeek<TSummary> where TSummary : struct
{
    bool IsReached(in TSummary summary);
}
