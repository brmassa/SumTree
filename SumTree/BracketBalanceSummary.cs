namespace SumTree;

/// <summary>Net depth and minimum prefix depth for one delimiter pair, including the empty prefix.</summary>
/// <remarks>
/// Combine in text order: (a.balance + b.balance, min(a.minimum, a.balance + b.minimum)).
/// IsBalanced validates this pair only; other delimiter types and language syntax are ignored.
/// Counters are signed 64-bit values; arithmetic throws OverflowException on overflow.
/// </remarks>
public readonly record struct DelimiterBalanceSummary(long Balance, long MinimumPrefixBalance)
{
    public static DelimiterBalanceSummary Identity => default;
    public bool IsBalanced => Balance == 0 && MinimumPrefixBalance >= 0;

    public static DelimiterBalanceSummary operator +(DelimiterBalanceSummary left, DelimiterBalanceSummary right) =>
        new(checked(left.Balance + right.Balance),
            Math.Min(left.MinimumPrefixBalance, checked(left.Balance + right.MinimumPrefixBalance)));
}

/// <summary>Raw prefix balances for parentheses, square brackets, and curly braces.</summary>
/// <remarks>
/// Includes delimiters inside strings and comments. Independent balances detect ")(" but
/// cannot reject crossing types such as "([)]". Matching and folding need further analysis.
/// Kept separate from TextSummary so applications opt into its additional node storage.
/// </remarks>
public readonly record struct BracketBalanceSummary(
    DelimiterBalanceSummary Parentheses,
    DelimiterBalanceSummary SquareBrackets,
    DelimiterBalanceSummary CurlyBraces)
{
    public static BracketBalanceSummary Identity => default;

    /// <summary>All three types end at zero depth without any negative prefix; does not validate mixed-type nesting.</summary>
    public bool HasBalancedPrefixes => Parentheses.IsBalanced && SquareBrackets.IsBalanced && CurlyBraces.IsBalanced;

    public static BracketBalanceSummary FromCharacter(char character) => character switch
    {
        '(' => new(new(1, 0), default, default),
        ')' => new(new(-1, -1), default, default),
        '[' => new(default, new(1, 0), default),
        ']' => new(default, new(-1, -1), default),
        '{' => new(default, default, new(1, 0)),
        '}' => new(default, default, new(-1, -1)),
        _ => Identity
    };

    public static BracketBalanceSummary operator +(BracketBalanceSummary left, BracketBalanceSummary right) =>
        new(left.Parentheses + right.Parentheses, left.SquareBrackets + right.SquareBrackets,
            left.CurlyBraces + right.CurlyBraces);
}

/// <summary>Opt-in typed operations for raw bracket prefix balances.</summary>
public readonly struct BracketBalanceSummaryOperations : ISummaryOperations<char, BracketBalanceSummary>
{
    public static BracketBalanceSummary Identity => BracketBalanceSummary.Identity;
    public static BracketBalanceSummary SummarizeElement(char element) => BracketBalanceSummary.FromCharacter(element);

    public static BracketBalanceSummary SummarizeSpan(ReadOnlySpan<char> elements)
    {
        long paren = 0, square = 0, curly = 0;
        long minParen = 0, minSquare = 0, minCurly = 0;
        foreach (var element in elements)
        {
            switch (element)
            {
                case '(': paren++; break;
                case ')': minParen = Math.Min(minParen, --paren); break;
                case '[': square++; break;
                case ']': minSquare = Math.Min(minSquare, --square); break;
                case '{': curly++; break;
                case '}': minCurly = Math.Min(minCurly, --curly); break;
            }
        }
        // A span is int-sized, so these long accumulators cannot overflow.
        return new(new(paren, minParen), new(square, minSquare), new(curly, minCurly));
    }

    public static BracketBalanceSummary Combine(in BracketBalanceSummary left, in BracketBalanceSummary right) => left + right;
}
