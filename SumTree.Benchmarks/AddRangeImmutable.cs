using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class AddRangeImmutable
{
    [Params(10, 100)] public int EditCount;

    [Benchmark(Description = "Rope")]
    public void RopeOfChar()
    {
        var lorem = BenchmarkData.LoremIpsum.ToRope();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(lorem);
        }
    }

    [Benchmark(Description = "ImmutableList\n.Builder")]
    public void ImmutableListBuilderOfChar()
    {
        var s = ImmutableList<char>.Empty.ToBuilder();
        s.AddRange(BenchmarkData.LoremIpsum);

        for (var i = 0; i < EditCount; i++)
        {
            s.AddRange(BenchmarkData.LoremIpsum);
        }
    }

    [Benchmark(Description = "ImmutableList")]
    public void ImmutableListOfChar()
    {
        var s = ImmutableList<char>.Empty.AddRange(BenchmarkData.LoremIpsum);
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(BenchmarkData.LoremIpsum);
        }
    }

    [Benchmark(Description = "ImmutableArray")]
    public void ImmutableArrayOfChar()
    {
        var s = ImmutableArray<char>.Empty.AddRange(BenchmarkData.LoremIpsum.AsSpan());
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(BenchmarkData.LoremIpsum.AsSpan());
        }
    }

    [Benchmark(Description = "SumTree (no dimensions)")]
    public void SumTreeNoDimensions()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
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
        for (var i = 0; i < EditCount; i++)
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
        for (var i = 0; i < EditCount; i++)
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
        for (var i = 0; i < EditCount; i++)
        {
            var loremToAdd = BenchmarkData.LoremIpsum.ToSumTreeWithLinesAndBrackets();
            s = s.AddRange(loremToAdd);
        }
    }
}