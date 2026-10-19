using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace Benchmarks;

/// <summary>Long session fragmentation and backing-buffer retention, outside BDN timing.</summary>
public static class EditorSessionDiagnostics
{
    const int Edits = 50000;

    public static void Run(string path)
    {
        var sessions = new List<object>();
        var retention = new List<object>();
        foreach (var size in new[] { 2097152, 16777216 })
        {
            foreach (var random in new[] { false, true })
            {
                sessions.Add(Session<TextSummary, TextSummaryOperations>(size, random, "LinesAndBrackets"));
                sessions.Add(Session<long, CharacterCountOperations>(size, random, "CharacterCountOnly"));
            }

            retention.Add(Retention(size, mutable: false));
            retention.Add(Retention(size, mutable: true));
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(new
        {
            CapturedUtc = DateTimeOffset.UtcNow,
            Runtime = RuntimeInformation.FrameworkDescription,
            Edits,
            Notes = "Single sequential traces with stopwatch overhead; not BDN confidence intervals. " +
                    "Each random replacement is RemoveRange plus InsertRange. " +
                    "All sessions retain the last 32 versions. Source construction and correctness checks are untimed. " +
                    "Weak-reference probes keep only the final 1024-character tree and its compact copy alive; " +
                    "no undo history or original tree is retained.",
            Sessions = sessions,
            Retention = retention
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Saved editor session diagnostics: {fullPath}");
    }

    static object Session<TSummary, TOps>(int size, bool random, string summary)
        where TSummary : struct
        where TOps : struct, ISummaryOperations<char, TSummary>
    {
        var source = EditorWorkload.CreateText(size);
        var initial = new SumTree<char, TSummary, TOps>(source.AsMemory());
        var positions = new int[Edits];
        var expected = source.ToCharArray();
        var generator = new Random(1729);
        for (var i = 0; i < Edits; i++)
        {
            positions[i] = random ? generator.Next(size) : size / 2 + i;
            if (random) { expected[positions[i]] = 'x'; }
        }

        var expectedText = random ? new string(expected) : source.Insert(size / 2, new string('x', Edits));
        var tree = initial;
        // Warm all edit paths before measuring this closed generic instantiation.
        var warming = Stopwatch.StartNew();
        do
        {
            tree = initial;
            for (var i = 0; i < 1000; i++) { tree = Apply(tree, positions[i], random); }
        } while (warming.ElapsedMilliseconds < 250);
        tree = initial;
        var history = new SumTree<char, TSummary, TOps>[32];
        var ticks = new long[Edits];
        var beforeGc = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Edits; i++)
        {
            var started = Stopwatch.GetTimestamp();
            tree = Apply(tree, positions[i], random);
            history[i % history.Length] = tree;
            ticks[i] = Stopwatch.GetTimestamp() - started;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var collections = Enumerable.Range(0, 3).Select(g => GC.CollectionCount(g) - beforeGc[g]).ToArray();
        if (tree.ToString() != expectedText || !EqualityComparer<TSummary>.Default.Equals(tree.Summary, TOps.SummarizeSpan(expectedText)))
        {
            throw new InvalidOperationException("Long session differs from the reference.");
        }
        double Microseconds(long tick) => tick * (1000000.0 / Stopwatch.Frequency);
        var firstThousandMean = ticks.Take(1000).Average(t => Microseconds(t));
        var lastThousandMean = ticks.TakeLast(1000).Average(t => Microseconds(t));
        Array.Sort(ticks);
        var lengths = new List<int>();
        foreach (var chunk in tree.GetChunks()) { lengths.Add(chunk.Length); }
        GC.KeepAlive(history);
        Console.WriteLine($"Validated {summary}, {(random ? "RandomReplace" : "LocalTyping")}, {size:N0} units: {tree.BufferCount:N0} leaves");
        return new
        {
            DocumentLength = size,
            Summary = summary,
            Pattern = random ? "RandomReplace" : "LocalTyping",
            MeanMicroseconds = ticks.Average(t => Microseconds(t)),
            P50Microseconds = Microseconds(ticks[Edits / 2 - 1]),
            P99Microseconds = Microseconds(ticks[(int)(Edits * 0.99) - 1]),
            MaxMicroseconds = Microseconds(ticks[^1]),
            FirstThousandMeanMicroseconds = firstThousandMean,
            LastThousandMeanMicroseconds = lastThousandMean,
            AllocatedBytesPerEdit = allocated / (double)Edits,
            GcCollections = collections,
            InitialLeaves = initial.BufferCount,
            FinalLeaves = tree.BufferCount,
            InitialDepth = initial.Depth,
            FinalDepth = tree.Depth,
            MeanLeafLength = lengths.Average(),
            MinimumLeafLength = lengths.Min(),
            LeavesUnder128Characters = lengths.Count(length => length < 128)
        };
    }

    static SumTree<char, TSummary, TOps> Apply<TSummary, TOps>(SumTree<char, TSummary, TOps> tree, int position, bool random)
        where TSummary : struct
        where TOps : struct, ISummaryOperations<char, TSummary> =>
        (random ? tree.RemoveRange(position, 1) : tree).InsertRange(position, "x".AsMemory());

    static object Retention(int size, bool mutable)
    {
        var probe = BuildSmallSurvivor(size, mutable);
        var aliveBefore = CollectAndCheck(probe);
        // Rebuild only the surviving content, dropping references to the source buffer.
        Compact(probe);
        var aliveAfter = CollectAndCheck(probe);
        if (probe.Tree.Length != 1024 || probe.Tree.ToString() != EditorWorkload.CreateText(size)[..1024])
        {
            throw new InvalidOperationException("Compaction changed the surviving content.");
        }
        GC.KeepAlive(probe);
        return new
        {
            OriginalLength = size,
            Source = mutable ? "CopiedCharArray" : "SharedString",
            SurvivingLength = 1024,
            OriginalBackingAliveAfterDeletion = aliveBefore,
            OriginalBackingAliveAfterCompaction = aliveAfter
        };
    }

    // Keep old tree values out of the caller's stack slots across the collection probes.
    sealed class RetentionProbe(TextTree tree, WeakReference backing)
    {
        internal TextTree Tree = tree;
        internal readonly WeakReference Backing = backing;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Compact(RetentionProbe probe)
    {
        probe.Tree = new TextTree(probe.Tree.ToString().AsMemory());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool CollectAndCheck(RetentionProbe probe)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var alive = probe.Backing.IsAlive;
        GC.KeepAlive(probe);
        return alive;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static RetentionProbe BuildSmallSurvivor(int size, bool mutable)
    {
        var source = EditorWorkload.CreateText(size);
        var tree = mutable ? new TextTree(source.ToCharArray()) : new TextTree(source.AsMemory());
        // Capture the actual owned backing array or string, not the mutable caller's array.
        var root = typeof(TextTree).GetField("_root", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(tree)!;
        while (root.GetType().Name.StartsWith("Branch", StringComparison.Ordinal))
        {
            root = root.GetType().GetProperty("Item", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(root, [0])!;
        }
        var memory = (ReadOnlyMemory<char>)root.GetType().GetField("Memory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(root)!;
        object backing = MemoryMarshal.TryGetString(memory, out var text, out _, out _) ? text!
            : MemoryMarshal.TryGetArray(memory, out var segment) ? segment.Array!
            : throw new InvalidOperationException("Unknown backing storage.");
        return new(tree.RemoveRange(1024, size - 1024), new WeakReference(backing));
    }
}

public readonly struct CharacterCountOperations : ISummaryOperations<char, long>
{
    public static long Identity => 0;
    public static long SummarizeElement(char element) => 1;
    public static long SummarizeSpan(ReadOnlySpan<char> elements) => elements.Length;
    public static long Combine(in long left, in long right) => left + right;
}
