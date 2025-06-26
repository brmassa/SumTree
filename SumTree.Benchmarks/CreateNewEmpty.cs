using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class CreateNewEmpty
{
    [Benchmark(Description = "Rope<char>.Empty")]
    public void EmptyRopeOfChar() => _ = Rope<char>.Empty;

    [Benchmark(Description = "new List<char>()")]
    public void EmptyListOfChar() => _ = new List<char>();

    [Benchmark(Description = "new StringBuilder()")]
    public void EmptyStringBuilder() => _ = new StringBuilder();

    [Benchmark(Description = "SumTree<char>.Empty")]
    public void EmptySumTree() => _ = SumTree<char>.Empty;

    [Benchmark(Description = "SumTree string.Empty (no dimensions)")]
    public void EmptySumTreeNoDimensions() => _ = string.Empty.ToSumTree();

    [Benchmark(Description = "SumTree string.Empty with Lines")]
    public void EmptySumTreeWithLines() => _ = string.Empty.ToSumTreeWithLines();

    [Benchmark(Description = "SumTree string.Empty with Brackets")]
    public void EmptySumTreeWithBrackets() => _ = string.Empty.ToSumTree(new BracketCountDimension());

    [Benchmark(Description = "SumTree string.Empty with Lines and Brackets")]
    public void EmptySumTreeWithLinesAndBrackets() => _ = string.Empty.ToSumTreeWithLinesAndBrackets();
}