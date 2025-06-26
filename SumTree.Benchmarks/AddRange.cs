using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class AddRange
{
    [Params(10, 100, 500)]
    public int EditCount;

    [Benchmark(Description = "Rope")]
    public void RopeOfChar()
    {
        var lorem = BenchmarkData.LoremIpsum.ToRope();
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            s = s.AddRange(lorem);
        }
    }

    [Benchmark(Description = "StringBuilder")]
    public void StringBuilder()
    {
        var s = new StringBuilder(BenchmarkData.LoremIpsum);
        for (int i = 0; i < EditCount; i++)
        {
            s.Append(BenchmarkData.LoremIpsum);
        }
    }

    [Benchmark(Description = "List")]
    public void ListOfChar()
    {
        var s = new List<char>(BenchmarkData.LoremIpsum);
        for (int i = 0; i < EditCount; i++)
        {
            s.AddRange(BenchmarkData.LoremIpsum);
        }
    }

    [Benchmark(Description = "SumTree (no dimensions)")]
    public void SumTreeNoDimensions()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree();
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            var loremToAdd = BenchmarkData.LoremIpsum.ToSumTree();
            s = s.AddRange(loremToAdd);
        }
    }

    [Benchmark(Description = "SumTree with Lines")]
    public void SumTreeWithLines()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTreeWithLines();
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            var loremToAdd = BenchmarkData.LoremIpsum.ToSumTreeWithLines();
            s = s.AddRange(loremToAdd);
        }
    }

    [Benchmark(Description = "SumTree with Brackets")]
    public void SumTreeWithBrackets()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree(new BracketCountDimension());
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            var loremToAdd = BenchmarkData.LoremIpsum.ToSumTree(new BracketCountDimension());
            s = s.AddRange(loremToAdd);
        }
    }

    [Benchmark(Description = "SumTree with Lines and Brackets")]
    public void SumTreeWithLinesAndBrackets()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTreeWithLinesAndBrackets();
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            var loremToAdd = BenchmarkData.LoremIpsum.ToSumTreeWithLinesAndBrackets();
            s = s.AddRange(loremToAdd);
        }
    }
}
