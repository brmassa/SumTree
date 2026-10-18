using BenchmarkDotNet.Attributes;
using Rope;

namespace Benchmarks;
[MemoryDiagnoser]
public class IndexOf
{
    static readonly Rope<char> FragmentedRope = BenchmarkData.LongDiffText1.Chunk(256).Select(c => c.ToRope()).Combine();

    static readonly Rope<char> FragmentedFind = FragmentedRope[(int)(FragmentedRope.Length * 0.33)..(int)(FragmentedRope.Length * 0.66)];

    static readonly Rope<char> Find = "[[New Haven Register]]".ToRope();

    [Params(10, 100, 1000, 10000)]
    public int Length;

    [Benchmark(Description = "Rope")]
    public void RopeOfChar()
    {
        _ = BenchmarkData.LongDiffText1[..Length].IndexOf(Find);
    }

    [BenchmarkCategory("Fragmented\nRope")]
    [Benchmark(Description = "IndexOf")]
    public void FragmentedRopeOfChar()
    {
        _ = FragmentedRope[..Length].IndexOf(Find);
    }

    [BenchmarkCategory("Fragmented\nRope")]
    [Benchmark(Description = "IndexOf (Fragmented Find)")]
    public void FragmentedRopeOfCharLarge()
    {
        _ = FragmentedRope[..Length].IndexOf(FragmentedFind);
    }

    [BenchmarkCategory("string")]
    [Benchmark(Description = "string")]
    public void String()
    {
        _ = BenchmarkData.LongDiffText1String[..Length].IndexOf("[[New Haven Register]]");
    }
}
