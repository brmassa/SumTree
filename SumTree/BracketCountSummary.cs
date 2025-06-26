namespace SumTree;

/// <summary>
/// Summary for tracking bracket counts and balance in code.
/// Useful for code editors to find matching brackets and maintain proper indentation.
/// </summary>
public readonly struct BracketCountSummary : IEquatable<BracketCountSummary>, IComparable<BracketCountSummary>
{
    /// <summary>
    /// The number of open parentheses '(' encountered.
    /// </summary>
    public int OpenParentheses { get; }

    /// <summary>
    /// The number of close parentheses ')' encountered.
    /// </summary>
    public int CloseParentheses { get; }

    /// <summary>
    /// The number of open square brackets '[' encountered.
    /// </summary>
    public int OpenSquareBrackets { get; }

    /// <summary>
    /// The number of close square brackets ']' encountered.
    /// </summary>
    public int CloseSquareBrackets { get; }

    /// <summary>
    /// The number of open curly braces '{' encountered.
    /// </summary>
    public int OpenCurlyBraces { get; }

    /// <summary>
    /// The number of close curly braces '}' encountered.
    /// </summary>
    public int CloseCurlyBraces { get; }

    public BracketCountSummary(
        int openParentheses, int closeParentheses,
        int openSquareBrackets, int closeSquareBrackets,
        int openCurlyBraces, int closeCurlyBraces)
    {
        OpenParentheses = openParentheses;
        CloseParentheses = closeParentheses;
        OpenSquareBrackets = openSquareBrackets;
        CloseSquareBrackets = closeSquareBrackets;
        OpenCurlyBraces = openCurlyBraces;
        CloseCurlyBraces = closeCurlyBraces;
    }

    /// <summary>
    /// Creates a summary for a single character.
    /// </summary>
    /// <param name="character">The character to summarize.</param>
    /// <returns>A summary for the single character.</returns>
    public static BracketCountSummary FromCharacter(char character)
    {
        return character switch
        {
            '(' => new BracketCountSummary(1, 0, 0, 0, 0, 0),
            ')' => new BracketCountSummary(0, 1, 0, 0, 0, 0),
            '[' => new BracketCountSummary(0, 0, 1, 0, 0, 0),
            ']' => new BracketCountSummary(0, 0, 0, 1, 0, 0),
            '{' => new BracketCountSummary(0, 0, 0, 0, 1, 0),
            '}' => new BracketCountSummary(0, 0, 0, 0, 0, 1),
            _ => Zero
        };
    }

    /// <summary>
    /// Combines two bracket count summaries.
    /// </summary>
    /// <param name="left">The left summary.</param>
    /// <param name="right">The right summary.</param>
    /// <returns>The combined summary.</returns>
    public static BracketCountSummary operator +(BracketCountSummary left, BracketCountSummary right)
    {
        return new BracketCountSummary(
            left.OpenParentheses + right.OpenParentheses,
            left.CloseParentheses + right.CloseParentheses,
            left.OpenSquareBrackets + right.OpenSquareBrackets,
            left.CloseSquareBrackets + right.CloseSquareBrackets,
            left.OpenCurlyBraces + right.OpenCurlyBraces,
            left.CloseCurlyBraces + right.CloseCurlyBraces);
    }

    /// <summary>
    /// Gets the zero/identity summary (no brackets).
    /// </summary>
    public static BracketCountSummary Zero => new(0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Checks if this summary represents empty content (no brackets).
    /// </summary>
    public bool IsEmpty => OpenParentheses == 0 && CloseParentheses == 0 &&
                           OpenSquareBrackets == 0 && CloseSquareBrackets == 0 &&
                           OpenCurlyBraces == 0 && CloseCurlyBraces == 0;

    /// <summary>
    /// Gets the net balance of parentheses (open - close).
    /// </summary>
    public int ParenthesesBalance => OpenParentheses - CloseParentheses;

    /// <summary>
    /// Gets the net balance of square brackets (open - close).
    /// </summary>
    public int SquareBracketsBalance => OpenSquareBrackets - CloseSquareBrackets;

    /// <summary>
    /// Gets the net balance of curly braces (open - close).
    /// </summary>
    public int CurlyBracesBalance => OpenCurlyBraces - CloseCurlyBraces;

    /// <summary>
    /// Gets the total number of open brackets of all types.
    /// </summary>
    public int TotalOpenBrackets => OpenParentheses + OpenSquareBrackets + OpenCurlyBraces;

    /// <summary>
    /// Gets the total number of close brackets of all types.
    /// </summary>
    public int TotalCloseBrackets => CloseParentheses + CloseSquareBrackets + CloseCurlyBraces;

    /// <summary>
    /// Checks if all bracket types are balanced.
    /// </summary>
    public bool IsBalanced => ParenthesesBalance == 0 && SquareBracketsBalance == 0 && CurlyBracesBalance == 0;

    public bool Equals(BracketCountSummary other)
    {
        return OpenParentheses == other.OpenParentheses &&
               CloseParentheses == other.CloseParentheses &&
               OpenSquareBrackets == other.OpenSquareBrackets &&
               CloseSquareBrackets == other.CloseSquareBrackets &&
               OpenCurlyBraces == other.OpenCurlyBraces &&
               CloseCurlyBraces == other.CloseCurlyBraces;
    }

    public override bool Equals(object? obj)
    {
        return obj is BracketCountSummary other && this.Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            OpenParentheses, CloseParentheses,
            OpenSquareBrackets, CloseSquareBrackets,
            OpenCurlyBraces, CloseCurlyBraces);
    }

    public int CompareTo(BracketCountSummary other)
    {
        var totalOpen = TotalOpenBrackets.CompareTo(other.TotalOpenBrackets);
        if (totalOpen != 0) return totalOpen;

        var totalClose = TotalCloseBrackets.CompareTo(other.TotalCloseBrackets);
        if (totalClose != 0) return totalClose;

        var parenOpen = OpenParentheses.CompareTo(other.OpenParentheses);
        if (parenOpen != 0) return parenOpen;

        var parenClose = CloseParentheses.CompareTo(other.CloseParentheses);
        if (parenClose != 0) return parenClose;

        var squareOpen = OpenSquareBrackets.CompareTo(other.OpenSquareBrackets);
        if (squareOpen != 0) return squareOpen;

        var squareClose = CloseSquareBrackets.CompareTo(other.CloseSquareBrackets);
        if (squareClose != 0) return squareClose;

        var curlyOpen = OpenCurlyBraces.CompareTo(other.OpenCurlyBraces);
        if (curlyOpen != 0) return curlyOpen;

        return CloseCurlyBraces.CompareTo(other.CloseCurlyBraces);
    }

    public static bool operator ==(BracketCountSummary left, BracketCountSummary right) => left.Equals(right);
    public static bool operator !=(BracketCountSummary left, BracketCountSummary right) => !left.Equals(right);
    public static bool operator <(BracketCountSummary left, BracketCountSummary right) => left.CompareTo(right) < 0;
    public static bool operator <=(BracketCountSummary left, BracketCountSummary right) => left.CompareTo(right) <= 0;
    public static bool operator >(BracketCountSummary left, BracketCountSummary right) => left.CompareTo(right) > 0;
    public static bool operator >=(BracketCountSummary left, BracketCountSummary right) => left.CompareTo(right) >= 0;

    public override string ToString()
    {
        return
            $"():{OpenParentheses}/{CloseParentheses}, []:{OpenSquareBrackets}/{CloseSquareBrackets}, {{}}:{OpenCurlyBraces}/{CloseCurlyBraces}";
    }
}

/// <summary>
/// Summary dimension for tracking bracket counts in character sequences.
/// </summary>
public class BracketCountDimension : SummaryDimensionBase<char, BracketCountSummary>
{
    /// <summary>
    /// Gets the singleton instance of the bracket count dimension.
    /// </summary>
    public static readonly BracketCountDimension Instance = new();

    /// <inheritdoc/>
    public override BracketCountSummary Identity => BracketCountSummary.Zero;

    /// <inheritdoc/>
    public override BracketCountSummary SummarizeElement(char element)
    {
        return BracketCountSummary.FromCharacter(element);
    }

    /// <inheritdoc/>
    public override BracketCountSummary SummarizeSpan(ReadOnlySpan<char> elements)
    {
        if (elements.IsEmpty)
            return Identity;

        int openParen = 0, closeParen = 0;
        int openSquare = 0, closeSquare = 0;
        int openCurly = 0, closeCurly = 0;

        for (int i = 0; i < elements.Length; i++)
        {
            switch (elements[i])
            {
                case '(':
                    openParen++;
                    break;
                case ')':
                    closeParen++;
                    break;
                case '[':
                    openSquare++;
                    break;
                case ']':
                    closeSquare++;
                    break;
                case '{':
                    openCurly++;
                    break;
                case '}':
                    closeCurly++;
                    break;
            }
        }

        return new BracketCountSummary(openParen, closeParen, openSquare, closeSquare, openCurly, closeCurly);
    }

    /// <inheritdoc/>
    public override BracketCountSummary Combine(BracketCountSummary left, BracketCountSummary right)
    {
        return left + right;
    }

    /// <inheritdoc/>
    public override bool CanExtend(BracketCountSummary summary)
    {
        return !summary.IsEmpty;
    }
}