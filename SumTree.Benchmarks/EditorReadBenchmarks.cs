using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace Benchmarks;

[MemoryDiagnoser]
public class EditorConstructionBenchmarks
{
    public IEnumerable<int> DocumentSizes => EditorWorkload.DocumentSizes;
    [ParamsSource(nameof(DocumentSizes))] public int DocumentLength { get; set; }
    string _text = "";

    [GlobalSetup]
    public void Setup()
    {
        _text = EditorWorkload.CreateText(DocumentLength);
        if (SumTree().ToString() != _text || SumTreeSummaries().ToString() != _text ||
            Rope().ToString() != _text || StringBuilder().ToString() != _text)
        {
            throw new InvalidOperationException("Construction did not preserve text.");
        }

        EditorWorkload.ValidateSummaries(SumTreeSummaries(), _text);
        EditorWorkload.ValidateSummaries(SumTreeTypedSummaries(), _text);
    }

    [Benchmark] public SumTree<char> SumTree() => _text.ToSumTree();
    [Benchmark] public SumTree<char> SumTreeSummaries() => _text.ToSumTreeWithLinesAndBrackets();
    [Benchmark] public TextTree SumTreeTypedSummaries() => new(_text.AsMemory());
    [Benchmark] public Rope<char> Rope() => _text.ToRope();
    [Benchmark] public StringBuilder StringBuilder() => new(_text);
}

[MemoryDiagnoser]
public class EditorReadBenchmarks
{
    public const int QueryCount = 128;
    public IEnumerable<int> DocumentSizes => EditorWorkload.DocumentSizes;
    [ParamsSource(nameof(DocumentSizes))] public int DocumentLength { get; set; }
    SumTree<char> _tree;
    TextTree _typed;
    readonly int[] _lineTargets = new int[QueryCount];
    readonly int[] _offsets = new int[QueryCount];
    long _expectedLineBreakSum;
    long _expectedLineColumnSum;
    string _text = "";

    [GlobalSetup]
    public void Setup()
    {
        var workload = new EditorWorkload(DocumentLength, EditorPattern.RandomEdits);
        workload.Validate();
        _tree = workload.InitialSummaryTree;
        _typed = workload.InitialTypedTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            _tree = workload.Apply(_tree, i);
            _typed = workload.Apply(_typed, i);
        }

        _text = workload.ExpectedText;
        var breaks = new List<int>();
        for (var i = 0; i < _text.Length; i++)
        {
            if (_text[i] == '\r' || (_text[i] == '\n' && (i == 0 || _text[i - 1] != '\r')))
            {
                breaks.Add(i);
            }
        }

        _expectedLineBreakSum = 0;
        _expectedLineColumnSum = 0;
        for (var i = 0; i < QueryCount; i++)
        {
            _lineTargets[i] = 1 + (int)((long)i * breaks.Count / QueryCount);
            _expectedLineBreakSum += breaks[_lineTargets[i] - 1];
            _offsets[i] = (int)((long)i * _text.Length / QueryCount);
            var prefix = LineNumberDimension.Instance.SummarizeSpan(_text.AsSpan(0, _offsets[i]));
            _expectedLineColumnSum += prefix.Lines + 1L + prefix.LastLineCharacters + 1L;
        }

        if (FindLineBreaks() != _expectedLineBreakSum || GetLineAndColumns() != _expectedLineColumnSum ||
            Materialize() != _text || Traverse() != _text.Sum(c => (long)c) ||
            TypedFindLineBreaks() != _expectedLineBreakSum || TypedMaterialize() != _text)
        {
            throw new InvalidOperationException("Read benchmark differs from the linear reference.");
        }
    }

    [Benchmark(OperationsPerInvoke = QueryCount)]
    public long FindLineBreaks()
    {
        var sum = 0L;
        foreach (var target in _lineTargets)
        {
            sum += _tree.FindPosition<LineNumberSummary>(summary => summary.Lines >= target);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = QueryCount)]
    public long GetLineAndColumns()
    {
        var sum = 0L;
        foreach (var offset in _offsets)
        {
            var position = _tree.GetLineAndColumn(offset);
            sum += position.Line + position.Column;
        }

        return sum;
    }

    [Benchmark] public LineNumberSummary RootSummary() => _tree.GetSummary<LineNumberSummary>();
    [Benchmark] public string Materialize() => _tree.ToString();
    [Benchmark] public TextSummary TypedRootSummary() => _typed.Summary;
    [Benchmark] public string TypedMaterialize() => _typed.ToString();

    [Benchmark(OperationsPerInvoke = QueryCount)]
    public long TypedFindLineBreaks()
    {
        var sum = 0L;
        foreach (var target in _lineTargets)
        {
            sum += _typed.FindPosition(new LineTarget(target));
        }

        return sum;
    }

    readonly struct LineTarget(int target) : ISummarySeek<TextSummary>
    {
        public bool IsReached(in TextSummary summary) => summary.Lines.Lines >= target;
    }

    [Benchmark]
    public long Traverse()
    {
        var sum = 0L;
        foreach (var character in _tree)
        {
            sum += character;
        }

        return sum;
    }
}

[MemoryDiagnoser]
public class EditorSnapshotBenchmarks
{
    public const int SnapshotCount = 32;
    public IEnumerable<int> DocumentSizes => EditorWorkload.DocumentSizes;
    [ParamsSource(nameof(DocumentSizes))] public int DocumentLength { get; set; }
    EditorWorkload _workload = null!;

    [GlobalSetup]
    public void Setup()
    {
        _workload = new EditorWorkload(DocumentLength, EditorPattern.MultipleCursors);
        _workload.Validate();
        var trees = SumTreeHistory();
        var typedTrees = TypedSumTreeHistory();
        var ropes = RopeHistory();
        var strings = StringBuilderHistory();
        var reference = new StringBuilder(_workload.InitialText);
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            _workload.Apply(reference, i);
            if (i < EditorWorkload.EditCount - SnapshotCount)
            {
                continue;
            }

            var slot = i % SnapshotCount;
            var expected = reference.ToString();
            if (trees[slot].ToString() != expected || typedTrees[slot].ToString() != expected ||
                ropes[slot].ToString() != expected || strings[slot] != expected)
            {
                throw new InvalidOperationException("A retained snapshot changed or contains incorrect text.");
            }

            EditorWorkload.ValidateSummaries(trees[slot], expected);
            EditorWorkload.ValidateSummaries(typedTrees[slot], expected);
        }
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public SumTree<char>[] SumTreeHistory()
    {
        var history = new SumTree<char>[SnapshotCount];
        var tree = _workload.InitialSummaryTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            history[i % SnapshotCount] = tree = _workload.Apply(tree, i);
        }

        return history;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public TextTree[] TypedSumTreeHistory()
    {
        var history = new TextTree[SnapshotCount];
        var tree = _workload.InitialTypedTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            history[i % SnapshotCount] = tree = _workload.Apply(tree, i);
        }

        return history;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public Rope<char>[] RopeHistory()
    {
        var history = new Rope<char>[SnapshotCount];
        var tree = _workload.InitialRope;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            history[i % SnapshotCount] = tree = _workload.Apply(tree, i);
        }

        return history;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public string[] StringBuilderHistory()
    {
        var history = new string[SnapshotCount];
        var text = new StringBuilder(_workload.InitialText);
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            _workload.Apply(text, i);
            history[i % SnapshotCount] = text.ToString();
        }

        return history;
    }
}
