using System.Text;
using Rope;
using SumTree;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace Benchmarks;

public enum EditorPattern
{
    LocalTyping,
    Backspace,
    Paste,
    RandomEdits,
    MultipleCursors
}

public readonly record struct EditorChange(int Position, int RemoveLength, string Text);

/// <summary>Deterministic editor traces shared by benchmarks, validation, and latency diagnostics.</summary>
public sealed class EditorWorkload
{
    public const int EditCount = 100;
    const string SourceLine = "    if (ready) { items[index] = Call(value); }\r\n";
    public static IEnumerable<int> DocumentSizes => ParseSizes();
    public string InitialText { get; }
    public string ExpectedText { get; }
    public EditorChange[] Changes { get; }
    public SumTree<char> InitialTree { get; }
    public SumTree<char> InitialSummaryTree { get; }
    public TextTree InitialTypedTree { get; }
    public Rope<char> InitialRope { get; }
    public SumTree<char>[] PreparedTrees { get; }
    public SumTree<char>[] PreparedSummaryTrees { get; }
    public TextTree[] PreparedTypedTrees { get; }
    public Rope<char>[] PreparedRopes { get; }

    public EditorWorkload(int documentLength, EditorPattern pattern)
    {
        InitialText = CreateText(documentLength);
        var reference = new StringBuilder(InitialText);
        var random = new Random(1729);
        Changes = new EditorChange[EditCount];
        for (var i = 0; i < Changes.Length; i++)
        {
            var change = pattern switch
            {
                EditorPattern.LocalTyping => new(documentLength / 2 + i, 0, i % 20 == 19 ? "\n" : "x"),
                EditorPattern.Backspace => new(documentLength / 2 - i - 1, 1, ""),
                EditorPattern.Paste => new(reference.Length / 2, 0, SourceLine),
                EditorPattern.RandomEdits => i % 3 == 0
                    ? new EditorChange(random.Next(reference.Length - 4), random.Next(1, 5), "")
                    : new EditorChange(random.Next(reference.Length + 1), 0, i % 7 == 0 ? "\r\n{}" : "xy"),
                EditorPattern.MultipleCursors => new((int)((long)(i % 8 + 1) * reference.Length / 9), 0,
                    i % 7 == 0 ? "\n" : "x"),
                _ => throw new ArgumentOutOfRangeException(nameof(pattern))
            };
            Changes[i] = change;
            reference.Remove(change.Position, change.RemoveLength).Insert(change.Position, change.Text);
        }

        ExpectedText = reference.ToString();
        InitialTree = InitialText.ToSumTree();
        InitialSummaryTree = InitialText.ToSumTreeWithLinesAndBrackets();
        InitialTypedTree = new TextTree(InitialText.AsMemory());
        InitialRope = InitialText.ToRope();
        PreparedTrees = Changes.Select(c => c.Text.ToSumTree()).ToArray();
        PreparedSummaryTrees = Changes.Select(c => c.Text.ToSumTreeWithLinesAndBrackets()).ToArray();
        PreparedTypedTrees = Changes.Select(c => new TextTree(c.Text.AsMemory())).ToArray();
        PreparedRopes = Changes.Select(c => c.Text.ToRope()).ToArray();
    }

    public static string CreateText(int length)
    {
        var text = new StringBuilder(length);
        while (text.Length < length)
        {
            text.Append(SourceLine.AsSpan(0, Math.Min(SourceLine.Length, length - text.Length)));
        }

        return text.ToString();
    }

    public SumTree<char> Apply(SumTree<char> tree, int index, bool prepared = false, bool summaries = false)
    {
        var change = Changes[index];
        if (change.RemoveLength > 0)
        {
            tree = tree.RemoveRange(change.Position, change.RemoveLength);
        }

        return prepared
            ? tree.InsertRange(change.Position, summaries ? PreparedSummaryTrees[index] : PreparedTrees[index])
            : tree.InsertRange(change.Position, change.Text.AsMemory());
    }

    public TextTree Apply(TextTree tree, int index, bool prepared = false)
    {
        var change = Changes[index];
        if (change.RemoveLength > 0)
        {
            tree = tree.RemoveRange(change.Position, change.RemoveLength);
        }

        return prepared ? tree.InsertRange(change.Position, PreparedTypedTrees[index]) :
            tree.InsertRange(change.Position, change.Text.AsMemory());
    }

    public Rope<char> Apply(Rope<char> tree, int index, bool prepared = false)
    {
        var change = Changes[index];
        if (change.RemoveLength > 0)
        {
            tree = tree[..change.Position] + tree[(change.Position + change.RemoveLength)..];
        }

        return change.Text.Length == 0 ? tree : tree.InsertRange(change.Position,
            prepared ? PreparedRopes[index] : change.Text.ToRope());
    }

    public void Apply(StringBuilder text, int index)
    {
        var change = Changes[index];
        text.Remove(change.Position, change.RemoveLength).Insert(change.Position, change.Text);
    }

    public void Validate()
    {
        var bare = InitialTree;
        var summaries = InitialSummaryTree;
        var prepared = InitialTree;
        var preparedSummaries = InitialSummaryTree;
        var typed = InitialTypedTree;
        var preparedTyped = InitialTypedTree;
        var rope = InitialRope;
        var preparedRope = InitialRope;
        var text = new StringBuilder(InitialText);
        for (var i = 0; i < Changes.Length; i++)
        {
            bare = Apply(bare, i);
            summaries = Apply(summaries, i);
            prepared = Apply(prepared, i, prepared: true);
            preparedSummaries = Apply(preparedSummaries, i, prepared: true, summaries: true);
            typed = Apply(typed, i);
            preparedTyped = Apply(preparedTyped, i, prepared: true);
            rope = Apply(rope, i);
            preparedRope = Apply(preparedRope, i, prepared: true);
            Apply(text, i);
            var expected = text.ToString();
            ValidateSummaries(summaries, expected);
            ValidateSummaries(preparedSummaries, expected);
            ValidateSummaries(typed, expected);
            ValidateSummaries(preparedTyped, expected);
        }

        foreach (var actual in new[] { bare.ToString(), summaries.ToString(), prepared.ToString(),
                     preparedSummaries.ToString(), typed.ToString(), preparedTyped.ToString(),
                     rope.ToString(), preparedRope.ToString(), text.ToString() })
        {
            if (actual != ExpectedText)
            {
                throw new InvalidOperationException("Editor trace result differs from StringBuilder reference.");
            }
        }

        if (InitialTree.ToString() != InitialText || InitialSummaryTree.ToString() != InitialText ||
            InitialRope.ToString() != InitialText || InitialTypedTree.ToString() != InitialText)
        {
            throw new InvalidOperationException("Editing changed the initial snapshot.");
        }
    }

    public static void ValidateSummaries(SumTree<char> tree, string text)
    {
        if (tree.GetSummary<LineNumberSummary>() != LineNumberDimension.Instance.SummarizeSpan(text) ||
            tree.GetSummary<BracketCountSummary>() != BracketCountDimension.Instance.SummarizeSpan(text))
        {
            throw new InvalidOperationException("Editor trace contains incorrect cached summaries.");
        }
    }

    public static void ValidateSummaries(TextTree tree, string text)
    {
        if (tree.Summary != TextSummaryOperations.SummarizeSpan(text))
        {
            throw new InvalidOperationException("Typed editor trace contains incorrect cached summaries.");
        }
    }

    static int[] ParseSizes()
    {
        var configured = Environment.GetEnvironmentVariable("SUMTREE_BENCHMARK_DOCUMENT_SIZES");
        var sizes = string.IsNullOrWhiteSpace(configured) ? [4096, 131072, 2097152] :
            configured.Split(',').Select(int.Parse).ToArray();
        if (sizes.Length == 0 || sizes.Any(size => size < 1024))
        {
            throw new ArgumentException("Document sizes must be at least 1024 UTF-16 code units.");
        }

        return sizes;
    }
}
