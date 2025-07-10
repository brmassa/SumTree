using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class InsertRange
{
    [Params(10, 100, 1000)] public int EditCount;

    [Benchmark]
    public void RopeOfChar()
    {
        var lorem = BenchmarkData.LoremIpsum.ToRope();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            s = s.InsertRange(321, lorem);
        }

        ////s.ToString();
    }

    [Benchmark]
    public void ListOfChar()
    {
        var lorem = BenchmarkData.LoremIpsum.ToCharArray();
        var s = new List<char>(lorem);
        for (var i = 0; i < EditCount; i++)
        {
            s.InsertRange(321, lorem);
        }

        ////s.ToString();
    }

    [Benchmark]
    public void StringBuilder()
    {
        var s = new StringBuilder(BenchmarkData.LoremIpsum);
        for (var i = 0; i < EditCount; i++)
        {
            s.Insert(321, BenchmarkData.LoremIpsum);
        }

        ////s.ToString();
    }

    [Benchmark(Description = "SumTree (no dimensions)")]
    public void SumTreeNoDimensions()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            var loremToInsert = BenchmarkData.LoremIpsum.AsMemory();
            s = s.InsertRange(321, loremToInsert);
        }
    }

    [Benchmark(Description = "SumTree with Lines")]
    public void SumTreeWithLines()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTreeWithLines();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            var loremToInsert = BenchmarkData.LoremIpsum.AsMemory();
            s = s.InsertRange(321, loremToInsert);
        }
    }

    [Benchmark(Description = "SumTree with Brackets")]
    public void SumTreeWithBrackets()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree(new BracketCountDimension());
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            var loremToInsert = BenchmarkData.LoremIpsum.AsMemory();
            s = s.InsertRange(321, loremToInsert);
        }
    }

    [Benchmark(Description = "SumTree with Lines and Brackets")]
    public void SumTreeWithLinesAndBrackets()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTreeWithLinesAndBrackets();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            var loremToInsert = BenchmarkData.LoremIpsum.AsMemory();
            s = s.InsertRange(321, loremToInsert);
        }
    }
}