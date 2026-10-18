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
        DiffOptions = DiffOptions<char>.LineLevel with
        {
            TimeoutSeconds = 0
        };
        DiffMatchPatch = new diff_match_patch()
        {
            Diff_Timeout = 0
        };
    }

    [Benchmark]
    public void RopeDiff()
    {
        _ = BenchmarkData.ShortDiffText1.Diff(BenchmarkData.ShortDiffText2, DiffOptions);
    }

    [Benchmark]
    public void DiffMatchPatchDiff()
    {
        DiffMatchPatch.diff_main(BenchmarkData.ShortDiffText1, BenchmarkData.ShortDiffText2);
    }
}
