using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace Benchmarks;

/// <summary>Large edits and viewport reads missing from the short editor traces.</summary>
[MemoryDiagnoser]
public class EditorBufferBenchmarks
{
    public IEnumerable<int> DocumentSizes => EditorWorkload.DocumentSizes;
    [ParamsSource(nameof(DocumentSizes))] public int DocumentLength { get; set; }
    TextTree _tree;
    TextTree _paste;
    string _payload = "";
    readonly char[] _viewport = new char[4096];

    [GlobalSetup]
    public void Setup()
    {
        var text = EditorWorkload.CreateText(DocumentLength);
        _tree = new TextTree(text.AsMemory());
        _payload = EditorWorkload.CreateText(65536);
        _paste = new TextTree(_payload.AsMemory());
        var inserted = Paste64K();
        if (inserted.ToString() != text.Insert(DocumentLength / 2, _payload)
            || inserted.Summary != TextSummaryOperations.SummarizeSpan(inserted.ToString()))
        {
            throw new InvalidOperationException("Large paste differs from the reference.");
        }
        if (DeleteMiddleHalf().ToString() != text.Remove(DocumentLength / 4, DocumentLength / 2))
        {
            throw new InvalidOperationException("Large deletion differs from the reference.");
        }
        ReadViewport();
        if (!_viewport.AsSpan().SequenceEqual(text.AsSpan((DocumentLength - _viewport.Length) / 2, _viewport.Length)))
        {
            throw new InvalidOperationException("Viewport differs from the reference.");
        }
    }

    [Benchmark] public TextTree Paste64K() => _tree.InsertRange(DocumentLength / 2, _payload.AsMemory());
    [Benchmark] public TextTree Paste64KPrepared() => _tree.InsertRange(DocumentLength / 2, _paste);
    [Benchmark] public TextTree DeleteMiddleHalf() => _tree.RemoveRange(DocumentLength / 4, DocumentLength / 2);
    [Benchmark] public void ReadViewport() => _tree.CopyTo((DocumentLength - _viewport.Length) / 2, _viewport);
}
