using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class CreateNewWithLength10
{
    private char[] array = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    [Benchmark(Description = "string.ToRope()")]
    public void RopeOfCharFromString() => _ = "0123456789".ToRope();

    [Benchmark(Description = "new Rope<char>(array)")]
    public void RopeOfCharFromArray() => _ = new Rope<char>(this.array);

    [Benchmark(Description = "new List<char>(array)")]
    public void ListOfCharFromArray() => _ = new List<char>(this.array);

    [Benchmark(Description = "new StringBuilder(string)")]
    public void StringBuilder() => _ = new StringBuilder("0123456789");


    [Benchmark(Description = "SumTree from string")]
    public void SumTreeFromString() => _ = "0123456789".ToSumTree();

    [Benchmark(Description = "SumTree from array")]
    public void SumTreeFromArray() => _ = this.array.ToSumTree();

    [Benchmark(Description = "SumTree with Lines")]
    public void SumTreeWithLines() => _ = "0123456789".ToSumTreeWithLines();

    [Benchmark(Description = "SumTree with Brackets")]
    public void SumTreeWithBrackets() => _ = "0123456789".ToSumTree(new BracketCountDimension());

    [Benchmark(Description = "SumTree with Lines and Brackets")]
    public void SumTreeWithLinesAndBrackets() => _ = "0123456789".ToSumTreeWithLinesAndBrackets();
}