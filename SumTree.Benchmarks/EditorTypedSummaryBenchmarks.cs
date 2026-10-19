using BenchmarkDotNet.Attributes;
using SumTree;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace Benchmarks;

/// <summary>The line/bracket metrics edit trace on the typed B+ rope path.</summary>
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
