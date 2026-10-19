# Editor benchmarks

The `Editor*` benchmarks provide a reproducible baseline for the editor backend
plan. Existing benchmarks remain available; their workloads and result units are
unchanged. In particular, legacy `InsertRange` compares prepared Rope insertion
with fresh-memory SumTree insertion and includes initial construction.

Performance-initiative results are kept locally in the gitignored
`docs/internal/benchmarks/` directory; do not commit result files.

## Workloads and units

All editor traces use a fixed seed, 100 edits, and generated source containing
brackets and CRLF. Default initial sizes are 4,096, 131,072, and 2,097,152 UTF-16
code units. Positions are UTF-16 offsets, not grapheme indices. Override sizes
with `SUMTREE_BENCHMARK_DOCUMENT_SIZES=4096,131072` (minimum 1,024).

| Class | Timed work | Result units |
| --- | --- | --- |
| `EditorInsertComparison` | Insert source snippets at the current midpoint; separate prepared subtrees and fresh string text, with/without summaries | Time and allocation per edit |
| `EditorTypedSummaryBenchmarks` | Dynamic and typed paths maintaining identical line/bracket metrics; fresh/prepared insertions | Time and allocation per edit |
| `EditorEditBenchmarks` | Local typing, backspace, paste, seeded random insertion/deletion, and eight interleaved cursor positions | Time and allocation per edit |
| `EditorConstructionBenchmarks` | Build each container from the same string | Per document |
| `EditorReadBenchmarks` | Find a line break, obtain line/column, retrieve root summary, traverse characters, or materialize text on an edited document | Per query for batches of 128 seeks; per call/document for the other methods |
| `EditorSnapshotBenchmarks` | Apply 100 edits and retain the last 32 versions in an undo ring | Time and allocation per edit |

Benchmark methods return their results. `GlobalSetup` validates content, cached
summaries, and retained versions outside timing. References use StringBuilder,
linear scans, and direct span summarization. Edits preserve the starting snapshots.
The standalone validation command also exercises every pattern on every size.

Initial immutable documents, trace generation, payload preparation, and reference
validation are outside edit timing. Prepared cases reuse payload trees built in
setup; fresh cases construct insertion nodes and summaries inside timing. Fresh
text is string-backed, so immutable character storage can be shared. This does
not measure copying caller-owned mutable buffers.

Rope and plain SumTree perform no editor summary work. `SumTreeSummaries` maintains
both line metrics and raw bracket counts. Compare their costs with that extra work
in mind. There is no artificial ratio baseline between these different capabilities.

The typed comparison uses the same text, edit positions, and summary semantics on
both paths. The typed path now uses immutable B+ nodes with inline child references
and local balancing; the dynamic path uses its existing binary rope. Results measure
the complete implementations and do not isolate layout, dictionary, boxing, or
dispatch costs individually. Historical typed-summary results describe the earlier
binary prototype and remain under `docs/internal/benchmarks/typed-summaries/`.

The B+ layout was evaluated with fanouts of 8 and 16 and leaf capacities of 512 and
1024. The current choice is 8 children and 1024-element leaves. Tuning runs, source
fingerprints, and their limitations are local artifacts under
`docs/internal/benchmarks/bplus/`; these constants are implementation choices.

`StringBuilderForkAndEdit` includes a copy of the initial mutable document for
each independent invocation. Immutable benchmarks fork by copying their tree
handle. This measures the cost of an independent edit branch; construction is
also measured separately. StringBuilder history materializes each version as a
string, while tree histories retain persistent handles. Allocation per edit is
different from memory still live after old versions become unreachable.

## Commands

Run from the repository root. Keep build verbosity quiet and redirect BDN's
intermediate output to a log:

```bash
dotnet build SumTree.slnx -c Release --verbosity quiet

# Correctness only: no performance conclusions from this command.
dotnet run --project SumTree.Benchmarks -c Release --no-build -- --editor-validate

# Focused insertion baseline: six methods at each configured document size.
dotnet run --project SumTree.Benchmarks -c Release --no-build -- \
  --filter '*EditorInsertComparison*' --job short --exporters json \
  --artifacts docs/internal/benchmarks/editor-baseline/inserts \
  > /tmp/sumtree-editor-inserts.log 2>&1

# Full editor coverage; use the normal job for release-quality comparisons.
dotnet run --project SumTree.Benchmarks -c Release --no-build -- \
  --filter '*Editor*Benchmarks*' '*EditorInsertComparison*' --exporters json \
  --artifacts docs/internal/benchmarks/editor-full \
  > /tmp/sumtree-editor-full.log 2>&1

# Current typed B+ path: identical metrics on dynamic and typed ropes.
dotnet run --project SumTree.Benchmarks -c Release --no-build -- \
  --filter '*EditorTypedSummaryBenchmarks*' --job short --exporters json \
  --artifacts docs/internal/benchmarks/bplus/final/inserts \
  > /tmp/sumtree-bplus-final-inserts.log 2>&1

# Per-edit trace latency and approximate retained memory; run separately from BDN.
dotnet run --project SumTree.Benchmarks -c Release --no-build -- \
  --editor-diagnostics docs/internal/benchmarks/editor-baseline/editor-diagnostics.json \
  > /tmp/sumtree-editor-diagnostics.log 2>&1
```

BDN validates optimized builds; the former global optimization-validator bypass
has been removed. Failed BDN reports produce a nonzero process exit code.
BDN result files contain runtime/hardware details, mean/error/deviation, allocations,
and GC rates. A short job establishes a preliminary baseline; use longer runs to
resolve small differences or broad confidence intervals. Keep the source revision
and configured document sizes with each result.

## Individual edit latency and retained memory

`--editor-diagnostics` records mean, p50, p95, p99, maximum, allocated bytes per
edit, and GC counts for every backend/pattern/size. Each row has three warmup
traces followed by five measured traces (500 individual edits). The clock and
delegate dispatch add overhead, and traces run sequentially. These diagnostic
percentiles can expose pauses; they are not independent steady-state BDN estimates
or predictions of a production editor's UI latency. Document creation/forking,
validation, and sample-array allocation are outside these edit measurements,
including StringBuilder's starting-document copy.

Retained-memory diagnostics use full-GC heap deltas while the last 32 versions
remain rooted after 100 edits at eight cursor positions. The initial document and
prepared payloads are already live before measurement and excluded from the delta.
These are approximate process-wide heap deltas, without process isolation; noise
can produce a negative delta. Use allocation profiles or an isolated heap study
before making a small retained-memory claim.

The diagnostics JSON records UTC time, Git HEAD, working-tree status, a SHA-256
fingerprint of tracked/unignored C# and project sources, runtime, OS, CPU, clock
frequency, sizes, trace counts, typed handle size, initial depth/leaf counts, and
the measurement caveats. Preserve this metadata
alongside BDN results; an uncommitted source tree cannot be identified by HEAD alone.

## Query and traversal comparisons

`EditorReadBenchmarks` compares direct typed prefix queries with slice-based queries
using the same offsets. `TypedTraverse` uses the concrete span enumerator;
`TypedInterfaceTraverse` measures the `IEnumerable<char>` adapter. Query methods
normalize by 128 queries; traversal methods measure one complete document.
`LineScanBenchmarks` compares scalar and span-search newline scanners on source,
long lines, and dense CR/LF/CRLF input, checking every prefix for equivalent results.
