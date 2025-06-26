using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class SplitThenConcat
{
    [Params(10, 100, 1000)]
    public int EditCount;

    [Benchmark]
    public void RopeOfChar()
    {
        var lorem = BenchmarkData.LoremIpsum.ToRope();
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            s = s[..321];
            s = s + lorem;
        }

        ////s.ToString();
    }

    [Benchmark]
    public void StringBuilder()
    {
        var s = new StringBuilder(BenchmarkData.LoremIpsum);
        for (int i = 0; i < EditCount; i++)
        {
            s.Remove(321, s.Length - 322); //  =  new StringBuilder(s.ToString()[..321]);
            s.Append(BenchmarkData.LoremIpsum);
        }

        ////s.ToString();
    }

    [Benchmark]
    public void ListOfChar()
    {
        var s = new List<char>(BenchmarkData.LoremIpsum);
        for (int i = 0; i < EditCount; i++)
        {
            s.RemoveRange(321, s.Count - 322); //  =  new StringBuilder(s.ToString()[..321]);
            s.AddRange(BenchmarkData.LoremIpsum);
        }

        ////s.ToString();
    }

    [Benchmark(Description = "SumTree (no dimensions)")]
    public void SumTreeNoDimensions()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree();
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            var (left, _) = s.SplitAt(321);
            s = left.AddRange(lorem);
        }

        ////s.ToString();
    }

    [Benchmark(Description = "SumTree with Lines")]
    public void SumTreeWithLines()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTreeWithLines();
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            var (left, _) = s.SplitAt(321);
            s = left.AddRange(lorem);
        }

        ////s.ToString();
    }

    [Benchmark(Description = "SumTree with Brackets")]
    public void SumTreeWithBrackets()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree(new BracketCountDimension());
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            var (left, _) = s.SplitAt(321);
            s = left.AddRange(lorem);
        }

        ////s.ToString();
    }

    [Benchmark(Description = "SumTree with Lines and Brackets")]
    public void SumTreeWithLinesAndBrackets()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTreeWithLinesAndBrackets();
        var s = lorem;
        for (int i = 0; i < EditCount; i++)
        {
            var (left, _) = s.SplitAt(321);
            s = left.AddRange(lorem);
        }

        ////s.ToString();
    }
}
