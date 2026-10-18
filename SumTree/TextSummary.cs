namespace SumTree;

/// <summary>Optional composite text metrics; bracket fields count occurrences, not valid nesting.</summary>
public readonly record struct TextSummary(LineNumberSummary Lines, BracketCountSummary Brackets);

/// <summary>Typed operations for the composite text example. Applications can supply their own operations.</summary>
public readonly struct TextSummaryOperations : ISummaryOperations<char, TextSummary>
{
    public static TextSummary Identity => new(LineNumberSummary.Zero, BracketCountSummary.Zero);

    public static TextSummary SummarizeElement(char element) =>
        new(LineNumberSummary.FromCharacter(element), BracketCountSummary.FromCharacter(element));

    public static TextSummary SummarizeSpan(ReadOnlySpan<char> elements) =>
        new(LineNumberDimension.Instance.SummarizeSpan(elements), BracketCountDimension.Instance.SummarizeSpan(elements));

    public static TextSummary Combine(in TextSummary left, in TextSummary right) =>
        new(left.Lines + right.Lines, left.Brackets + right.Brackets);
}
