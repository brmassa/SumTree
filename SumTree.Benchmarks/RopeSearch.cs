namespace Benchmarks;

[MemoryDiagnoser]
public class RopeSearch
{
    static readonly Rope<int> Ints = (from x in Enumerable.Range(0, 100)
                                      select Enumerable.Range(0, 100).Select(i => x + i).ToRope()).ToRope().Combine();

    static readonly Rope<int> PrefixInts = Enumerable.Range(0, 1000).ToRope();

    static readonly Rope<int> FindInts = Enumerable.Range(0, 1000).Select(i => i + 5000).ToRope();

    [Benchmark]
    public void CommonPrefixLengthLargeFind() => _ = Ints.CommonPrefixLength(PrefixInts);

    [Benchmark]
    public void IndexOfLargeFind() => _ = Ints.IndexOf(FindInts);

    [Benchmark]
    public void LastIndexOfLargeFind() => _ = Ints.LastIndexOf(FindInts);
}
