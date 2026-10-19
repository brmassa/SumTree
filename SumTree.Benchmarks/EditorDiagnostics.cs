using System.Security.Cryptography;
using System.Text;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace Benchmarks;

/// <summary>Trace latency and approximate retained-memory diagnostics, separate from BDN throughput.</summary>
public static class EditorDiagnostics
{
    const int WarmupTraces = 3;
    const int MinimumWarmupMilliseconds = 250;
    const int MeasuredTraces = 5;

    public static void ValidateAll()
    {
        foreach (var size in EditorWorkload.DocumentSizes)
        {
            foreach (var pattern in Enum.GetValues<EditorPattern>())
            {
                new EditorWorkload(size, pattern).Validate();
            }

            new EditorConstructionBenchmarks { DocumentLength = size }.Setup();
            new EditorInsertComparison { DocumentLength = size }.Setup();
            new EditorTypedSummaryBenchmarks { DocumentLength = size }.Setup();
            new EditorReadBenchmarks { DocumentLength = size }.Setup();
            new EditorSnapshotBenchmarks { DocumentLength = size }.Setup();
            Console.WriteLine($"Validated editor traces, construction, reads, and retained snapshots: {size:N0} UTF-16 units");
        }
    }

    public static void Run(string path)
    {
        var latencies = new List<LatencyResult>();
        var retention = new List<RetentionResult>();
        var layouts = new List<object>();
        foreach (var size in EditorWorkload.DocumentSizes)
        {
            foreach (var pattern in Enum.GetValues<EditorPattern>())
            {
                var workload = new EditorWorkload(size, pattern);
                workload.Validate();
                if (pattern == EditorPattern.LocalTyping)
                {
                    layouts.Add(new
                    {
                        DocumentLength = size,
                        workload.InitialTypedTree.Depth,
                        workload.InitialTypedTree.BufferCount
                    });
                }
                latencies.Add(Measure("SumTreeTypedSummaries", size, pattern, () => workload.InitialTypedTree,
                    (tree, i) => workload.Apply(tree, i), workload.ExpectedText));
                latencies.Add(Measure("Rope", size, pattern, () => workload.InitialRope,
                    (tree, i) => workload.Apply(tree, i), workload.ExpectedText));
                latencies.Add(Measure("StringBuilder", size, pattern, () => new StringBuilder(workload.InitialText),
                    (text, i) => { workload.Apply(text, i); return text; }, workload.ExpectedText));

                if (pattern == EditorPattern.MultipleCursors)
                {
                    retention.Add(new("SumTreeTypedSummaries", size, RetainedBytes(workload.InitialTypedTree,
                        (tree, i) => workload.Apply(tree, i))));
                    retention.Add(new("Rope", size, RetainedBytes(workload.InitialRope,
                        (tree, i) => workload.Apply(tree, i))));
                    var builder = new StringBuilder(workload.InitialText);
                    retention.Add(new("StringBuilderSnapshots", size, RetainedBytes(workload.InitialText,
                        (_, i) => { workload.Apply(builder, i); return builder.ToString(); })));
                }
            }

            Console.WriteLine($"Measured editor diagnostics: {size:N0} UTF-16 units");
        }

        var report = new
        {
            CapturedUtc = DateTimeOffset.UtcNow,
            Commit = Git("rev-parse", "HEAD").Trim(),
            WorkingTree = Git("status", "--porcelain").Trim(),
            SourceSha256 = SourceHash(),
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            LogicalProcessors = Environment.ProcessorCount,
            Cpu = CpuDescription(),
            Stopwatch.Frequency,
            DocumentSizes = EditorWorkload.DocumentSizes.ToArray(),
            EditorWorkload.EditCount,
            WarmupTraces,
            MinimumWarmupMilliseconds,
            MeasuredTraces,
            EditorSnapshotBenchmarks.SnapshotCount,
            TypedHandleBytes = Unsafe.SizeOf<TextTree>(),
            TypedDocuments = layouts,
            Notes = "Sequential diagnostic samples include stopwatch/delegate overhead and natural GC; " +
                    "500 samples per row, not independent steady-state BDN estimates. " +
                    "Document construction/forking, validation, and sample-array allocation are outside edit timing. " +
                    "Fresh insertion uses string-backed memory; UTF-16 offsets. " +
                    "Retained bytes are approximate full-GC heap deltas for the last 32 versions after 100 edits, " +
                    "excluding the initial document and prepared payloads. No process isolation; heap noise is possible.",
            Latencies = latencies,
            Retention = retention
        };
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Saved editor diagnostics: {fullPath}");
    }

    static LatencyResult Measure<T>(string backend, int size, EditorPattern pattern, Func<T> initial,
        Func<T, int, T> apply, string expected) where T : notnull
    {
        var warmup = Stopwatch.StartNew();
        for (var trace = 0; trace < WarmupTraces || warmup.ElapsedMilliseconds < MinimumWarmupMilliseconds; trace++)
        {
            var warm = initial();
            for (var i = 0; i < EditorWorkload.EditCount; i++)
            {
                warm = apply(warm, i);
            }

            ValidateFinal(warm, expected);
        }

        var ticks = new long[MeasuredTraces * EditorWorkload.EditCount];
        var allocated = 0L;
        var collections = new int[3];
        for (var trace = 0; trace < MeasuredTraces; trace++)
        {
            var current = initial();
            var beforeGc = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < EditorWorkload.EditCount; i++)
            {
                var started = Stopwatch.GetTimestamp();
                current = apply(current, i);
                ticks[trace * EditorWorkload.EditCount + i] = Stopwatch.GetTimestamp() - started;
            }

            allocated += GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            for (var generation = 0; generation < 3; generation++)
            {
                collections[generation] += GC.CollectionCount(generation) - beforeGc[generation];
            }

            ValidateFinal(current, expected);
        }

        Array.Sort(ticks);
        double Nanoseconds(long value) => value * (1_000_000_000.0 / Stopwatch.Frequency);
        double Percentile(double percentile) => Nanoseconds(ticks[(int)Math.Ceiling(percentile * ticks.Length) - 1]);
        return new(backend, size, pattern.ToString(), ticks.Length,
            ticks.Average(value => Nanoseconds(value)), Percentile(0.50), Percentile(0.95), Percentile(0.99),
            Nanoseconds(ticks[^1]), allocated / (double)ticks.Length, collections);
    }

    static void ValidateFinal<T>(T value, string expected) where T : notnull
    {
        if (value.ToString() != expected)
        {
            throw new InvalidOperationException("Timed trace produced incorrect text.");
        }

        if (value is TextTree typed)
        {
            EditorWorkload.ValidateSummaries(typed, expected);
        }
    }

    static long RetainedBytes<T>(T initial, Func<T, int, T> apply)
    {
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var history = new T[EditorSnapshotBenchmarks.SnapshotCount];
        var current = initial;
        for (var i = 0; i < EditorWorkload.EditCount; i++)
        {
            history[i % history.Length] = current = apply(current, i);
        }

        var after = GC.GetTotalMemory(forceFullCollection: true);
        GC.KeepAlive(history);
        GC.KeepAlive(initial);
        GC.KeepAlive(apply);
        return after - before;
    }

    static string Git(params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var result = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git failed: {error}");
        }

        return result;
    }

    static string SourceHash()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var root = Git("rev-parse", "--show-toplevel").Trim();
        var files = Git("-C", root, "ls-files", "--cached", "--others", "--exclude-standard", "-z").Split('\0',
            StringSplitOptions.RemoveEmptyEntries).Distinct().Order(StringComparer.Ordinal);
        foreach (var file in files.Where(f => Path.GetExtension(f) is ".cs" or ".csproj" or ".props" or ".slnx"))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file + "\0"));
            hash.AppendData(File.ReadAllBytes(Path.Combine(root, file)));
            hash.AppendData([0]);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    static string CpuDescription() => File.Exists("/proc/cpuinfo")
        ? File.ReadLines("/proc/cpuinfo").FirstOrDefault(line => line.StartsWith("model name", StringComparison.Ordinal)) ?? "Unknown"
        : Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown";

    // Consumed by System.Text.Json when serializing the report, not referenced in code.
    // ReSharper disable once NotAccessedPositionalProperty.Local
    sealed record LatencyResult(string Backend, int DocumentLength, string Pattern, int Samples,
        double MeanNanoseconds, double P50Nanoseconds, double P95Nanoseconds, double P99Nanoseconds,
        double MaxNanoseconds, double AllocatedBytesPerEdit, int[] GcCollections);
    // Consumed by System.Text.Json when serializing the report, not referenced in code.
    // ReSharper disable once NotAccessedPositionalProperty.Local
    sealed record RetentionResult(string Backend, int DocumentLength, long ApproximateAdditionalRetainedBytes);
}
