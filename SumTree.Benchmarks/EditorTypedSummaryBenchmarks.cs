using BenchmarkDotNet.Attributes;
using SumTree;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace Benchmarks;

/// <summary>Same edit trace and line/bracket metrics on the dynamic and typed binary rope paths.</summary>
[MemoryDiagnoser]
public class EditorTypedSummaryBenchmarks
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
    public SumTree<char> DynamicFresh()
    {
        var tree = _workload.InitialSummaryTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public SumTree<char> DynamicPrepared()
    {
        var tree = _workload.InitialSummaryTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i, prepared: true, summaries: true);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public TextTree TypedFresh()
    {
        var tree = _workload.InitialTypedTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i);
        }

        return tree;
    }

    [Benchmark(OperationsPerInvoke = EditorWorkload.EditCount)]
    public TextTree TypedPrepared()
    {
        var tree = _workload.InitialTypedTree;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            tree = _workload.Apply(tree, i, prepared: true);
        }

        return tree;
    }
}
