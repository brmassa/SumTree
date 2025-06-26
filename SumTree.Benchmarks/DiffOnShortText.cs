// Copyright 2010 Google Inc.
// All Right Reserved.

using BenchmarkDotNet.Attributes;
using DiffMatchPatch;
using Rope.Compare;

namespace Benchmarks;

[MemoryDiagnoser]
public class DiffOnShortText
{
    public DiffOptions<char> DiffOptions { get; }

    public diff_match_patch DiffMatchPatch { get; }

    public DiffOnShortText()
    {
        this.DiffOptions = DiffOptions<char>.LineLevel with
        {
            TimeoutSeconds = 0
        };
        this.DiffMatchPatch = new diff_match_patch()
        {
            Diff_Timeout = 0
        };
    }

    [Benchmark]
    public void RopeDiff()
    {
        _ = BenchmarkData.ShortDiffText1.Diff(BenchmarkData.ShortDiffText2, this.DiffOptions);
    }

    [Benchmark]
    public void DiffMatchPatchDiff()
    {
        this.DiffMatchPatch.diff_main(BenchmarkData.ShortDiffText1, BenchmarkData.ShortDiffText2);
    }
}
