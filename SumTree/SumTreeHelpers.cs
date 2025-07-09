// Copyright 2025 Bruno Massa - SumTree Helper Extensions

namespace SumTree;

/// <summary>
/// Helper extension methods for creating SumTree instances with common dimensions.
/// </summary>
public static class SumTreeHelpers
{
    // Cache dimension dictionaries to avoid repeated allocations
    private static readonly Dictionary<Type, object> LinesDimensions = new()
    {
        [typeof(LineNumberSummary)] = LineNumberDimension.Instance
    };

    private static readonly Dictionary<Type, object> LinesAndBracketsDimensions = new()
    {
        [typeof(LineNumberSummary)] = LineNumberDimension.Instance,
        [typeof(BracketCountSummary)] = BracketCountDimension.Instance
    };

    private static readonly Dictionary<Type, object> EmptyDimensions = new();

    /// <summary>
    /// Creates a SumTree from a string with line number tracking.
    /// </summary>
    /// <param name="text">The text to create the SumTree from.</param>
    /// <returns>A new SumTree with line number dimension.</returns>
    public static SumTree<char> ToSumTreeWithLines(this string text)
    {
        return new SumTree<char>(text.AsMemory(), LinesDimensions);
    }

    /// <summary>
    /// Creates a SumTree from a string with both line number and bracket count tracking.
    /// </summary>
    /// <param name="text">The text to create the SumTree from.</param>
    /// <returns>A new SumTree with line number and bracket count dimensions.</returns>
    public static SumTree<char> ToSumTreeWithLinesAndBrackets(this string text)
    {
        return new SumTree<char>(text.AsMemory(), LinesAndBracketsDimensions);
    }

    /// <summary>
    /// Creates a SumTree with the specified dimensions.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="memory">The memory to create the SumTree from.</param>
    /// <param name="dimensions">The dimensions to track.</param>
    /// <returns>A new SumTree.</returns>
    public static SumTree<T> ToSumTree<T>(this ReadOnlyMemory<T> memory, params object[] dimensions)
        where T : IEquatable<T>
    {
        // Optimize for empty dimensions case
        if (dimensions.Length == 0)
        {
            return new SumTree<T>(memory, EmptyDimensions);
        }

        var dimensionDict = new Dictionary<Type, object>(dimensions.Length);

        foreach (var dimension in dimensions)
        {
            var dimensionType = dimension.GetType();
            var interfaces = dimensionType.GetInterfaces();
            var summaryInterface = interfaces.FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISummaryDimension<,>));

            if (summaryInterface != null)
            {
                var summaryType = summaryInterface.GetGenericArguments()[1];
                dimensionDict[summaryType] = dimension;
            }
        }

        return new SumTree<T>(memory, dimensionDict);
    }

    /// <summary>
    /// Creates a SumTree from an enumerable with the specified dimensions.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="elements">The elements to create the SumTree from.</param>
    /// <param name="dimensions">The dimensions to track.</param>
    /// <returns>A new SumTree.</returns>
    public static SumTree<T> ToSumTree<T>(this IEnumerable<T> elements, params object[] dimensions)
        where T : IEquatable<T>
    {
        var memory = elements.ToArray().AsMemory();
        return ((ReadOnlyMemory<T>)memory).ToSumTree(dimensions);
    }

    /// <summary>
    /// Finds the line at the specified line number (1-based).
    /// </summary>
    /// <param name="sumTree">The SumTree to search in.</param>
    /// <param name="lineNumber">The line number to find (1-based).</param>
    /// <returns>The index where the line starts, or -1 if not found.</returns>
    public static long FindLineStart(this SumTree<char> sumTree, int lineNumber)
    {
        if (lineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");

        if (lineNumber == 1)
            return 0;

        var targetLines = lineNumber - 1; // Convert to 0-based for internal counting
        return sumTree.FindPosition<LineNumberSummary>(summary => summary.Lines >= targetLines);
    }

    /// <summary>
    /// Finds the line at the specified line number and returns the line content.
    /// </summary>
    /// <param name="sumTree">The SumTree to search in.</param>
    /// <param name="lineNumber">The line number to find (1-based).</param>
    /// <returns>The content of the line, or null if not found.</returns>
    public static string? GetLineContent(this SumTree<char> sumTree, int lineNumber)
    {
        var lineStart = sumTree.FindLineStart(lineNumber);
        if (lineStart == -1)
            return null;

        // Find the end of the line
        var lineEnd = sumTree.Length;
        for (var i = lineStart; i < sumTree.Length; i++)
        {
            if (sumTree.ElementAt(i) == '\n')
            {
                lineEnd = i;
                break;
            }
        }

        var lineLength = lineEnd - lineStart;
        var lineSlice = sumTree.Slice(lineStart, lineLength);
        return new string(lineSlice.ToArray());
    }

    /// <summary>
    /// Gets the line and column position (1-based) for the specified index.
    /// </summary>
    /// <param name="sumTree">The SumTree to query.</param>
    /// <param name="index">The index to get the position for.</param>
    /// <returns>A tuple containing the line and column (both 1-based).</returns>
    public static (int Line, int Column) GetLineAndColumn(this SumTree<char> sumTree, long index)
    {
        if (index < 0 || index > sumTree.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (index == 0)
            return (1, 1);

        // Get the summary up to the specified index
        var slice = sumTree.Slice(0, index);
        var summary = slice.GetSummary<LineNumberSummary>();

        var line = summary.Lines + 1; // Convert to 1-based
        var column = summary.LastLineCharacters + 1; // Convert to 1-based

        return (line, column);
    }

    /// <summary>
    /// Finds the position of the Nth occurrence of an open bracket.
    /// </summary>
    /// <param name="sumTree">The SumTree to search in.</param>
    /// <param name="bracketType">The type of bracket to find ('(', '[', or '{').</param>
    /// <param name="occurrence">The occurrence number (1-based).</param>
    /// <returns>The index of the Nth open bracket, or -1 if not found.</returns>
    public static long FindNthOpenBracket(this SumTree<char> sumTree, char bracketType, int occurrence)
    {
        if (occurrence <= 0)
            throw new ArgumentOutOfRangeException(nameof(occurrence), "Occurrence must be positive.");

        return sumTree.FindPosition<BracketCountSummary>(summary =>
        {
            var count = bracketType switch
            {
                '(' => summary.OpenParentheses,
                '[' => summary.OpenSquareBrackets,
                '{' => summary.OpenCurlyBraces,
                _ => throw new ArgumentException($"Invalid bracket type: {bracketType}")
            };
            return count >= occurrence;
        });
    }

    /// <summary>
    /// Finds the position of the Nth occurrence of a close bracket.
    /// </summary>
    /// <param name="sumTree">The SumTree to search in.</param>
    /// <param name="bracketType">The type of bracket to find (')', ']', or '}').</param>
    /// <param name="occurrence">The occurrence number (1-based).</param>
    /// <returns>The index of the Nth close bracket, or -1 if not found.</returns>
    public static long FindNthCloseBracket(this SumTree<char> sumTree, char bracketType, int occurrence)
    {
        if (occurrence <= 0)
            throw new ArgumentOutOfRangeException(nameof(occurrence), "Occurrence must be positive.");

        return sumTree.FindPosition<BracketCountSummary>(summary =>
        {
            var count = bracketType switch
            {
                ')' => summary.CloseParentheses,
                ']' => summary.CloseSquareBrackets,
                '}' => summary.CloseCurlyBraces,
                _ => throw new ArgumentException($"Invalid bracket type: {bracketType}")
            };
            return count >= occurrence;
        });
    }

    /// <summary>
    /// Checks if the brackets are balanced up to the specified index.
    /// </summary>
    /// <param name="sumTree">The SumTree to check.</param>
    /// <param name="index">The index to check up to.</param>
    /// <returns>True if brackets are balanced, false otherwise.</returns>
    public static bool AreBracketsBalanced(this SumTree<char> sumTree, long index)
    {
        if (index < 0 || index > sumTree.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var slice = sumTree.Slice(0, index);
        var summary = slice.GetSummary<BracketCountSummary>();
        return summary.IsBalanced;
    }

    /// <summary>
    /// Inserts text at the specified line and column position.
    /// </summary>
    /// <param name="sumTree">The SumTree to insert into.</param>
    /// <param name="line">The line number (1-based).</param>
    /// <param name="column">The column number (1-based).</param>
    /// <param name="text">The text to insert.</param>
    /// <returns>A new SumTree with the text inserted.</returns>
    public static SumTree<char> InsertAtLineColumn(this SumTree<char> sumTree, int line, int column, string text)
    {
        var lineStart = sumTree.FindLineStart(line);
        if (lineStart == -1)
            throw new ArgumentOutOfRangeException(nameof(line), "Line number not found.");

        var insertIndex = lineStart + column - 1; // Convert to 0-based index
        if (insertIndex > sumTree.Length)
            insertIndex = sumTree.Length;

        return sumTree.InsertRange(insertIndex, text.AsMemory());
    }

    /// <summary>
    /// Gets diagnostic information about the SumTree's summaries.
    /// </summary>
    /// <param name="sumTree">The SumTree to get diagnostics for.</param>
    /// <returns>A string containing diagnostic information.</returns>
    public static string GetDiagnostics<T>(this SumTree<T> sumTree) where T : IEquatable<T>
    {
        var diagnostics = new List<string>
        {
            $"Length: {sumTree.Length}",
            $"IsEmpty: {sumTree.IsEmpty}",
            $"DimensionCount: {sumTree.DimensionCount}",
            $"Depth: {sumTree.Depth}",
            $"BufferCount: {sumTree.BufferCount}",
            $"IsBalanced: {sumTree.IsBalanced}"
        };

        if (sumTree.HasDimension<LineNumberSummary>())
        {
            var lineSummary = sumTree.GetSummary<LineNumberSummary>();
            diagnostics.Add($"Lines: {lineSummary.Lines + 1}"); // +1 for 1-based counting
            diagnostics.Add($"LastLineChars: {lineSummary.LastLineCharacters}");
            diagnostics.Add($"TotalChars: {lineSummary.TotalCharacters}");
        }

        if (sumTree.HasDimension<BracketCountSummary>())
        {
            var bracketSummary = sumTree.GetSummary<BracketCountSummary>();
            diagnostics.Add($"Brackets: {bracketSummary}");
            diagnostics.Add($"Balanced: {bracketSummary.IsBalanced}");
        }

        return string.Join(Environment.NewLine, diagnostics);
    }
}
