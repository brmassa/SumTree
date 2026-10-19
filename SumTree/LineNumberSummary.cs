namespace SumTree;

/// <summary>
/// Summary for tracking line numbers and character positions in text.
/// Useful for text editors to quickly navigate to specific lines.
/// </summary>
/// <remarks>
/// Character counts are UTF-16 code units. Counters are signed 32-bit values;
/// composition throws OverflowException when a counter exceeds that range.
/// </remarks>
public readonly struct LineNumberSummary : IEquatable<LineNumberSummary>, IComparable<LineNumberSummary>
{
    /// <summary>
    /// The number of line breaks in this segment. CR and LF each count as one;
    /// a CRLF pair counts as one even when split across segments.
    /// </summary>
    public int Lines { get; }

    /// <summary>
    /// The number of characters in the last line (after the last newline).
    /// If there are no newlines, this is the total character count.
    /// </summary>
    public int LastLineCharacters { get; }

    /// <summary>
    /// The total number of characters in this segment.
    /// </summary>
    public int TotalCharacters { get; }

    /// <summary>Whether this nonempty segment starts with LF, for CRLF boundary merging.</summary>
    public bool StartsWithLineFeed { get; }

    /// <summary>Whether this nonempty segment ends with CR, for CRLF boundary merging.</summary>
    public bool EndsWithCarriageReturn { get; }

    /// <summary>
    /// Creates a summary. Supply boundary flags when representing actual text that
    /// starts with LF or ends with CR; seek targets may omit them.
    /// </summary>
    public LineNumberSummary(int lines, int lastLineCharacters, int totalCharacters,
        bool startsWithLineFeed = false, bool endsWithCarriageReturn = false)
    {
        Lines = lines;
        LastLineCharacters = lastLineCharacters;
        TotalCharacters = totalCharacters;
        StartsWithLineFeed = startsWithLineFeed;
        EndsWithCarriageReturn = endsWithCarriageReturn;
    }

    /// <summary>
    /// Creates a summary for a single character.
    /// </summary>
    /// <param name="character">The character to summarize.</param>
    /// <returns>A summary for the single character.</returns>
    public static LineNumberSummary FromCharacter(char character)
    {
        return character is '\r' or '\n'
            ? new LineNumberSummary(1, 0, 1, character == '\n', character == '\r')
            : new LineNumberSummary(0, 1, 1);
    }

    /// <summary>
    /// Summarizes a span of characters in a single pass, treating CR, LF and CRLF as line breaks.
    /// </summary>
    /// <param name="elements">The span to summarize.</param>
    /// <returns>The summary of the span.</returns>
    public static LineNumberSummary SummarizeSpan(ReadOnlySpan<char> elements)
    {
        if (elements.IsEmpty)
        {
            return Zero;
        }

        var lines = 0;
        var lastLineCharacters = 0;
        var totalCharacters = elements.Length;

        for (var i = 0; i < elements.Length; i++)
        {
            var ch = elements[i];

            if (ch == '\n')
            {
                lines++;
                lastLineCharacters = 0;
            }
            else if (ch == '\r')
            {
                // Handle \r\n as a single line break
                if (i + 1 < elements.Length && elements[i + 1] == '\n')
                {
                    lines++;
                    lastLineCharacters = 0;
                    i++; // Skip the \n part of \r\n
                }
                else
                {
                    // Standalone \r is treated as a line break
                    lines++;
                    lastLineCharacters = 0;
                }
            }
            else
            {
                lastLineCharacters++;
            }
        }

        return new LineNumberSummary(lines, lastLineCharacters, totalCharacters,
            elements[0] == '\n', elements[^1] == '\r');
    }

    /// <summary>
    /// Combines two line number summaries.
    /// </summary>
    /// <param name="left">The left summary.</param>
    /// <param name="right">The right summary.</param>
    /// <returns>The combined summary.</returns>
    public static LineNumberSummary operator +(LineNumberSummary left, LineNumberSummary right)
    {
        if (left.IsEmpty)
        {
            return right;
        }

        if (right.IsEmpty)
        {
            return left;
        }

        var totalLines = checked(left.Lines + right.Lines -
                         (left.EndsWithCarriageReturn && right.StartsWithLineFeed ? 1 : 0));
        var lastLineChars =
            right.Lines > 0 ? right.LastLineCharacters : checked(left.LastLineCharacters + right.LastLineCharacters);
        var totalChars = checked(left.TotalCharacters + right.TotalCharacters);

        return new LineNumberSummary(totalLines, lastLineChars, totalChars,
            left.StartsWithLineFeed, right.EndsWithCarriageReturn);
    }

    /// <summary>
    /// Gets the zero/identity summary (empty text).
    /// </summary>
    public static LineNumberSummary Zero => new(0, 0, 0);

    /// <summary>
    /// Checks if this summary represents empty content.
    /// </summary>
    public bool IsEmpty => Lines == 0 && LastLineCharacters == 0 && TotalCharacters == 0;

    public bool Equals(LineNumberSummary other)
    {
        return Lines == other.Lines &&
               LastLineCharacters == other.LastLineCharacters &&
               TotalCharacters == other.TotalCharacters &&
               StartsWithLineFeed == other.StartsWithLineFeed &&
               EndsWithCarriageReturn == other.EndsWithCarriageReturn;
    }

    public override bool Equals(object? obj)
    {
        return obj is LineNumberSummary other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Lines, LastLineCharacters, TotalCharacters,
            StartsWithLineFeed, EndsWithCarriageReturn);
    }

    public int CompareTo(LineNumberSummary other)
    {
        var linesComparison = Lines.CompareTo(other.Lines);
        if (linesComparison != 0)
        {
            return linesComparison;
        }

        var lastLineComparison = LastLineCharacters.CompareTo(other.LastLineCharacters);
        if (lastLineComparison != 0)
        {
            return lastLineComparison;
        }

        // Ordering describes a seek position; boundary flags only affect composition.
        return TotalCharacters.CompareTo(other.TotalCharacters);
    }

    public static bool operator ==(LineNumberSummary left, LineNumberSummary right) => left.Equals(right);
    public static bool operator !=(LineNumberSummary left, LineNumberSummary right) => !left.Equals(right);
    public static bool operator <(LineNumberSummary left, LineNumberSummary right) => left.CompareTo(right) < 0;
    public static bool operator <=(LineNumberSummary left, LineNumberSummary right) => left.CompareTo(right) <= 0;
    public static bool operator >(LineNumberSummary left, LineNumberSummary right) => left.CompareTo(right) > 0;
    public static bool operator >=(LineNumberSummary left, LineNumberSummary right) => left.CompareTo(right) >= 0;

    public override string ToString()
    {
        return $"Lines: {Lines}, LastLineChars: {LastLineCharacters}, TotalChars: {TotalCharacters}";
    }
}
