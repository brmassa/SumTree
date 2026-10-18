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

    Rope<char> _ropeX;
    Rope<char> _ropeY;
    StringBuilder? _sbX;
    ReadOnlyMemory<char> _sbY;

    string? _strX;
    string? _strY;

    ImmutableArray<char> _arrayX;

    ImmutableArray<char> _arrayY;

    SumTree<char> _sumTreeX;
    SumTree<char> _sumTreeY;
    SumTree<char> _sumTreeWithLinesX;
    SumTree<char> _sumTreeWithLinesY;

    [GlobalSetup]
    public void Setup()
    {
        _ropeX = BenchmarkData.LoremIpsum.ToRope().Slice(Length);
        _ropeY = BenchmarkData.LoremIpsum.ToRope().Slice(Length);
        _sbY = BenchmarkData.LoremIpsum[..Length].AsMemory();
        _sbX = new StringBuilder(BenchmarkData.LoremIpsum[..Length]);
        _strX = BenchmarkData.LoremIpsum[..Length];
        _strY = BenchmarkData.LoremIpsum[..Length];
        _arrayX = [.. BenchmarkData.LoremIpsum[..Length].AsSpan()];
        _arrayY = [.. BenchmarkData.LoremIpsum[..Length].AsSpan()];
        _sumTreeX = BenchmarkData.LoremIpsum[..Length].ToSumTree();
        _sumTreeY = BenchmarkData.LoremIpsum[..Length].ToSumTree();
        _sumTreeWithLinesX = BenchmarkData.LoremIpsum[..Length].ToSumTreeWithLines();
        _sumTreeWithLinesY = BenchmarkData.LoremIpsum[..Length].ToSumTreeWithLines();
    }

    [Benchmark(Description = "Rope<char>")]
    public void RopeOfChar()
    {
        _ = _ropeX.Equals(_ropeY);
    }

    [Benchmark(Description = "StringBuilder")]
    public void StringBuilder()
    {
        _ = _sbX!.Equals(_sbY.Span);
    }

    [Benchmark(Description = "string")]
    public void String()
    {
        _ = _strX!.Equals(_strY);
    }

    [Benchmark(Description = "SumTree<char>")]
    public void SumTree()
    {
        _ = _sumTreeX.Equals(_sumTreeY);
    }

    [Benchmark(Description = "SumTree<char> with Lines")]
    public void SumTreeWithLines()
    {
        _ = _sumTreeWithLinesX.Equals(_sumTreeWithLinesY);
    }
}
