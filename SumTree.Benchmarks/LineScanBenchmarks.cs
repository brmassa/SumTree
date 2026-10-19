namespace Benchmarks;

/// <summary>Compares newline scanners with identical CR/LF/CRLF semantics.</summary>
[MemoryDiagnoser]
public class LineScanBenchmarks
{
    [Params("Source", "LongLine", "DenseBreaks")] public string Pattern { get; set; } = "Source";
    string _text = "";

    [GlobalSetup]
    public void Setup()
    {
        _text = Pattern switch
        {
            "Source" => EditorWorkload.CreateText(1024),
            "LongLine" => new string('x', 1024),
            _ => string.Concat(Enumerable.Repeat("\r\n\r\n\nx", 171))
        };
        for (var length = 0; length <= _text.Length; length++)
        {
            if (Scan(_text.AsSpan(0, length)) != LineNumberSummary.SummarizeSpan(_text.AsSpan(0, length)))
            {
                throw new InvalidOperationException("Newline scanners disagree.");
            }
        }
    }

    [Benchmark(Baseline = true)] public LineNumberSummary Scalar() => LineNumberSummary.SummarizeSpan(_text);
    [Benchmark] public LineNumberSummary SpanSearch() => Scan(_text);

    static LineNumberSummary Scan(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return LineNumberSummary.Zero;
        }

        var offset = 0;
        var lines = 0;
        var lastEnd = 0;
        while (offset < text.Length)
        {
            var next = text[offset..].IndexOfAny('\r', '\n');
            if (next < 0)
            {
                break;
            }

            offset += next;
            var cr = text[offset++] == '\r';
            if (cr && offset < text.Length && text[offset] == '\n')
            {
                offset++;
            }

            lines++;
            lastEnd = offset;
        }
        return new LineNumberSummary(lines, text.Length - lastEnd, text.Length, text[0] == '\n', text[^1] == '\r');
    }
}
