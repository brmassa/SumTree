using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class GetHashCode
{
    [Params(10, 100, 1000, 10000)]
    public int Length;

    [Benchmark(Description = "Rope<char>")]
    public void RopeOfChar()
    {
        _ = BenchmarkData.LoremIpsum.ToRope().Slice(Length).GetHashCode();
    }


    [Benchmark(Description = "string")]
    public void String()
    {
        _ = BenchmarkData.LoremIpsum[..Length].GetHashCode();
    }


    [Benchmark(Description = "ImmutableArray<char>")]
    public void ImmutableArrayOfChar()
    {
        var s = ImmutableArray<char>.Empty.AddRange(BenchmarkData.LoremIpsum[..Length].AsSpan());
        _ = s.GetHashCode();
    }

    [Benchmark(Description = "SumTree<char>")]
    public void SumTree()
    {
        _ = BenchmarkData.LoremIpsum[..Length].ToSumTree().GetHashCode();
    }

    [Benchmark(Description = "SumTree<char> with Lines")]
    public void SumTreeWithLines()
    {
        _ = BenchmarkData.LoremIpsum[..Length].ToSumTreeWithLines().GetHashCode();
    }
}
