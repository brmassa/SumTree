using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;
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

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public SumTree<char> SumTreeFresh()
    {
        var tree = _workload.InitialTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public SumTree<char> SumTreePrepared()
    {
        var tree = _workload.InitialTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i, prepared: true);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public SumTree<char> SumTreeSummariesFresh()
    {
        var tree = _workload.InitialSummaryTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public SumTree<char> SumTreeSummariesPrepared()
    {
        var tree = _workload.InitialSummaryTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i, prepared: true, summaries: true);
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
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public SumTree<char> SumTree()
    {
        var tree = _workload.InitialTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public SumTree<char> SumTreeSummaries()
    {
        var tree = _workload.InitialSummaryTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
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
