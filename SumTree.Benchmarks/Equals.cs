using System.Collections.Immutable;
using System.Text;
using BenchmarkDotNet.Attributes;
using Rope;
using SumTree;

namespace Benchmarks;

[MemoryDiagnoser]
public class Equals
{
    [Params(10, 100, 1000, 10000)]
    public int Length;

    private Rope<char> ropeX;
    private Rope<char> ropeY;
    private StringBuilder? sbX;
    private ReadOnlyMemory<char> sbY;

    private string? strX;
    private string? strY;

    private ImmutableArray<char> arrayX;

    private ImmutableArray<char> arrayY;

    private SumTree<char> sumTreeX;
    private SumTree<char> sumTreeY;
    private SumTree<char> sumTreeWithLinesX;
    private SumTree<char> sumTreeWithLinesY;

    [GlobalSetup]
    public void Setup()
    {
        this.ropeX = BenchmarkData.LoremIpsum.ToRope().Slice(Length);
        this.ropeY = BenchmarkData.LoremIpsum.ToRope().Slice(Length);
        this.sbY = BenchmarkData.LoremIpsum[..Length].AsMemory();
        this.sbX = new StringBuilder(BenchmarkData.LoremIpsum[..Length]);
        this.strX = BenchmarkData.LoremIpsum[..Length];
        this.strY = BenchmarkData.LoremIpsum[..Length];
        this.arrayX = ImmutableArray<char>.Empty.AddRange(BenchmarkData.LoremIpsum[..Length].AsSpan());
        this.arrayY = ImmutableArray<char>.Empty.AddRange(BenchmarkData.LoremIpsum[..Length].AsSpan());
        this.sumTreeX = BenchmarkData.LoremIpsum[..Length].ToSumTree();
        this.sumTreeY = BenchmarkData.LoremIpsum[..Length].ToSumTree();
        this.sumTreeWithLinesX = BenchmarkData.LoremIpsum[..Length].ToSumTreeWithLines();
        this.sumTreeWithLinesY = BenchmarkData.LoremIpsum[..Length].ToSumTreeWithLines();
    }

    [Benchmark(Description = "Rope<char>")]
    public void RopeOfChar()
    {
        _ = this.ropeX.Equals(this.ropeY);
    }

    [Benchmark(Description = "StringBuilder")]
    public void StringBuilder()
    {
        _ = this.sbX!.Equals(this.sbY.Span);
    }

    [Benchmark(Description = "string")]
    public void String()
    {
        _ = this.strX!.Equals(strY);
    }

    [Benchmark(Description = "SumTree<char>")]
    public void SumTree()
    {
        _ = this.sumTreeX.Equals(this.sumTreeY);
    }

    [Benchmark(Description = "SumTree<char> with Lines")]
    public void SumTreeWithLines()
    {
        _ = this.sumTreeWithLinesX.Equals(this.sumTreeWithLinesY);
    }
}
