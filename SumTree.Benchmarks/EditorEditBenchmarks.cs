using System.Text;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace Benchmarks;

[MemoryDiagnoser]
public class EditorInsertComparison
{
    public IEnumerable<int> DocumentSizes => EditorWorkload.DocumentSizes;
    [ParamsSource(nameof(DocumentSizes))] public int DocumentLength { get; set; }
    EditorWorkload _workload = null!;

    [GlobalSetup]
    public void Setup()
    {
        _workload = new EditorWorkload(DocumentLength, EditorPattern.Paste);
        _workload.Validate();
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public Rope<char> RopeFresh()
    {
        var tree = _workload.InitialRope;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public Rope<char> RopePrepared()
    {
        var tree = _workload.InitialRope;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i, prepared: true);
        }

        return tree;
    }
}

[MemoryDiagnoser]
public class EditorEditBenchmarks
{
    public IEnumerable<int> DocumentSizes => EditorWorkload.DocumentSizes;
    // BenchmarkDotNet assigns [Params] properties through reflection in parameter setup.
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    [ParamsSource(nameof(DocumentSizes))] public int DocumentLength { get; set; }
    // BenchmarkDotNet assigns [Params] properties through reflection in parameter setup.
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    [ParamsAllValues] public EditorPattern Pattern { get; set; }
    EditorWorkload _workload = null!;

    [GlobalSetup]
    public void Setup()
    {
        _workload = new EditorWorkload(DocumentLength, Pattern);
        _workload.Validate();
        if (StringEdits() != _workload.ExpectedText)
        {
            throw new InvalidOperationException("String edit trace differs from the reference.");
        }
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public Rope<char> Rope()
    {
        var tree = _workload.InitialRope;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public TextTree SumTreeTypedSummaries()
    {
        var tree = _workload.InitialTypedTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public string StringEdits()
    {
        var text = _workload.InitialText;
        foreach (var change in _workload.Changes)
        {
            text = text.Remove(change.Position, change.RemoveLength).Insert(change.Position, change.Text);
        }

        return text;
    }

    // Includes cloning the initial mutable document for an independent invocation.
    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public StringBuilder StringBuilderForkAndEdit()
    {
        var text = new StringBuilder(_workload.InitialText);
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            _workload.Apply(text, i);
        }

        return text;
    }
}
