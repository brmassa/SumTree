namespace SumTree;

/// <summary>UTF-16 text helpers for the example composite text summary. Lines and columns are one-based.</summary>
public static class TypedTextExtensions
{
    /// <summary>Returns the position of a prefix endpoint. Both endpoints inside/after a CRLF map to column one.</summary>
    public static (int Line, int Column) GetLineAndColumn<TOps>(this SumTree<char, TextSummary, TOps> tree, long index)
        where TOps : struct, ISummaryOperations<char, TextSummary>
    {
        var summary = tree.GetPrefixSummary(index).Lines;
        return (summary.Lines + 1, summary.LastLineCharacters + 1);
    }

    public static long FindLineStart<TOps>(this SumTree<char, TextSummary, TOps> tree, int lineNumber)
        where TOps : struct, ISummaryOperations<char, TextSummary>
    {
        if (lineNumber <= 0) throw new ArgumentOutOfRangeException(nameof(lineNumber));
        if (lineNumber == 1) return 0;
        var end = tree.FindPosition(new LineTarget(lineNumber - 1));
        if (end < 0) return -1;
        var start = end + 1;
        if (tree[end] == '\r' && start < tree.Length && tree[start] == '\n') start++;
        return start;
    }

    /// <summary>Returns a line without its CR, LF, or CRLF terminator; null if the line does not exist.</summary>
    public static string? GetLineContent<TOps>(this SumTree<char, TextSummary, TOps> tree, int lineNumber)
        where TOps : struct, ISummaryOperations<char, TextSummary>
    {
        var start = tree.FindLineStart(lineNumber);
        if (start < 0) return null;
        var end = tree.FindPosition(new LineTarget(lineNumber));
        if (end < 0) end = tree.Length;
        return string.Create(checked((int)(end - start)), (tree, start),
            static (destination, state) => state.tree.CopyTo(state.start, destination));
    }

    readonly struct LineTarget(int target) : ISummarySeek<TextSummary>
    {
        public bool IsReached(in TextSummary summary) => summary.Lines.Lines >= target;
    }
}
