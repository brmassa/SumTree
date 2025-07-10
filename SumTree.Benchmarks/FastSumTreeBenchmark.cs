using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class FastSumTreeBenchmark
{
    [Params(10, 500)]
    public int EditCount;

    [Benchmark(Description = "Rope", Baseline = true)]
    public void RopeOfChar()
    {
        var lorem = BenchmarkData.LoremIpsum.ToRope();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(lorem);
        }
    }

    [Benchmark(Description = "StringBuilder")]
    public void StringBuilder()
    {
        var s = new StringBuilder(BenchmarkData.LoremIpsum);
        for (var i = 0; i < EditCount; i++)
        {
            s.Append(BenchmarkData.LoremIpsum);
        }
    }

    [Benchmark(Description = "List<char>")]
    public void ListOfChar()
    {
        var s = new List<char>(BenchmarkData.LoremIpsum);
        for (var i = 0; i < EditCount; i++)
        {
            s.AddRange(BenchmarkData.LoremIpsum);
        }
    }

    [Benchmark(Description = "SumTree")]
    public void SumTreeOfChar()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            var loremToAdd = BenchmarkData.LoremIpsum.ToSumTree();
            s = s.AddRange(loremToAdd);
        }
    }

    [Benchmark(Description = "SumTree (reuse)")]
    public void SumTreeOfCharReuse()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(lorem);
        }
    }

    [Benchmark(Description = "SumTree (memory)")]
    public void SumTreeOfCharMemory()
    {
        var loremMemory = BenchmarkData.LoremIpsum.AsMemory();
        var s = loremMemory.ToSumTree();
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(loremMemory);
        }
    }

    [Benchmark(Description = "SumTree (array)")]
    public void SumTreeOfCharArray()
    {
        var loremArray = BenchmarkData.LoremIpsum.ToCharArray();
        var s = loremArray.ToSumTree();
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(loremArray);
        }
    }
    
    [Benchmark(Description = "FastSumTree")]
    public void FastSumTreeOfChar()
    {
        var lorem = BenchmarkData.LoremIpsum.ToSumTree();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            var loremToAdd = BenchmarkData.LoremIpsum.ToFastSumTree();
            s = s.AddRange(loremToAdd);
        }
    }

    [Benchmark(Description = "FastSumTree (reuse)")]
    public void FastSumTreeOfCharReuse()
    {
        var lorem = BenchmarkData.LoremIpsum.ToFastSumTree();
        var s = lorem;
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(lorem);
        }
    }

    [Benchmark(Description = "FastSumTree (memory)")]
    public void FastSumTreeOfCharMemory()
    {
        var loremMemory = BenchmarkData.LoremIpsum.AsMemory();
        var s = loremMemory.ToFastSumTree();
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(loremMemory);
        }
    }

    [Benchmark(Description = "FastSumTree (array)")]
    public void FastSumTreeOfCharArray()
    {
        var loremArray = BenchmarkData.LoremIpsum.ToCharArray();
        var s = loremArray.ToFastSumTree();
        for (var i = 0; i < EditCount; i++)
        {
            s = s.AddRange(loremArray);
        }
    }
}
