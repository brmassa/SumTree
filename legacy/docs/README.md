# SumTree

![logo](docs/logo.png)

[![Build Status](https://github.com/brmassa/SumTree/actions/workflows/continuous-integration.yml/badge.svg)](https://github.com/brmassa/SumTree/actions)
[![License](https://img.shields.io/github/license/brmassa/SumTree.svg)](https://github.com/brmassa/SumTree/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/com.BrunoMassa.SumTree.svg)](https://www.nuget.org/packages/BrunoMassa.SumTree)
[![downloads](https://img.shields.io/nuget/dt/com.BrunoMassa.SumTree)](https://www.nuget.org/packages/BrunoMassa.SumTree)
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/C0C0AE5SC)

SumTree is a rope — an immutable tree of element chunks — with summary dimensions attached to the nodes. If you're building a text editor (or anything else that edits large sequences and asks questions about them), it's a decent fit: edits are O(log n) and share structure with the previous version instead of copying the buffer, and summaries like line counts stay up to date as you edit.

It works with any `T`, not just `char`. The design follows the classic [Ropes: an Alternative to Strings](https://www.cs.rit.edu/usr/local/pub/jeh/courses/QUARTERS/FP/Labs/CedarRope/rope-paper.pdf) paper, with summaries added on top.

## Getting started

```powershell
dotnet add package BrunoMassa.SumTree
```

```csharp
using SumTree;

SumTree<char> text = "Hello, World!".ToSumTree();
SumTree<int> numbers = new[] { 1, 2, 3, 4, 5 }.ToSumTree();

SumTree<char> combined = text + " How are you?".ToSumTree();
Console.WriteLine(combined.ToString()); // "Hello, World! How are you?"
```

`SumTree<char>` mostly behaves like you'd hope: `Length`, `IndexOf`, `Contains`, `Slice`, `RemoveRange`, `Insert`, `Replace`, plus the usual LINQ methods. Construction is flexible — from an array, a `ReadOnlyMemory<T>`, a pair of trees, or `SumTree<T>.Empty`.

## Summaries

This is the part a plain rope doesn't give you. Register a dimension and the tree maintains it incrementally:

```csharp
SumTree<char> document = @"Line 1
Line 2
Line 3".ToSumTreeWithLines();

var (line, column) = document.GetLineAndColumn(10); // line 2, column 4
string lineContent = document.GetLineContent(1);    // "Line 1"

SumTree<char> modified = document.InsertAtLineColumn(2, 1, "Modified ");
```

Two dimensions ship with the library:

- `LineNumberSummary` — CR/LF/CRLF line metrics, line/column navigation.
- `BracketCountSummary` — raw bracket occurrence counts and nth-occurrence queries (`FindNthOpenBracket`).

One caveat worth knowing up front: bracket counts are raw text. Strings and comments are included, and equal open/close counts say nothing about nesting — `)(` and `([)]` both come out "balanced". Anything syntax-aware needs a real parser.

Your own metrics just need `ISummaryDimension<T, TSummary>` (or `SummaryDimensionBase` as a starting point):

```csharp
public class VowelCountDimension : SummaryDimensionBase<char, int>
{
    public override int Identity => 0;
    public override int SummarizeElement(char element) =>
        "aeiouAEIOU".Contains(element) ? 1 : 0;
    public override int Combine(int left, int right) => left + right;
}

var text = "Hello World".ToSumTree(new VowelCountDimension());
int vowels = text.GetSummary<int>(); // 3
```

The one hard requirement: combination must be associative and agree with element-by-element composition, whatever the chunk boundaries happen to be. Implementations should be pure and thread-safe.

## Typed trees

`SumTree<T, TSummary, TOps>` is a leaner variant for when your summary layout is fixed at compile time: one value-type summary per node, static generic operations, no runtime registration. It uses immutable B+ nodes (up to eight children inline, uniform leaf depth) that split and join by copying boundary paths and sharing the rest. Leaves hold up to 1024 elements, and small neighboring leaves get coalesced into a 512-element buffer.

```csharp
var text = new SumTree<char, TextSummary, TextSummaryOperations>("Hello\r\nWorld".AsMemory());
var edited = text.InsertRange(5, "!".AsMemory());
Console.WriteLine(edited.Summary.Lines.Lines); // 1
```

A few things to watch for on this path:

- Index and range arguments are validated strictly — no legacy clamping.
- `GetEnumerator()` returns a `ref struct`, so plain `foreach` works (allocation-free), but if you need an `IEnumerator<T>` — say, across an `await` — go through `((IEnumerable<T>)tree).GetEnumerator()`.
- Seek predicates (via `Func<TSummary, bool>` or the allocation-free `ISummarySeek<TSummary>`) must be monotonic: once a prefix reaches the target, every longer prefix must too. Results are inclusive indices; a miss returns -1.
- The built-in summaries use 32-bit counters that throw `OverflowException` instead of wrapping. For very large documents, bring wider counters of your own. `BracketBalanceSummary` is the exception, with checked 64-bit math.

`TextSummary` is a ready-made composite of the line and bracket-count metrics. `BracketBalanceSummary` (net and minimum prefix depth per bracket type, useful for catching premature closes like `)(`) is deliberately separate so you only pay for the node storage if you opt in. Mixed-type crossing like `([)]` still needs a stack or parser.

## Snapshots and threading

The trees are immutable, so old versions stay valid as you edit. Two things to keep straight:

- Constructors and insertions copy mutable input buffers (string-backed memory is shared). Copies are shallow — elements and summaries must stay immutable if versions are shared across threads.
- `FromBorrowedMemory` skips the copy. In exchange, you guarantee the input stays valid and unchanged until every derived snapshot is gone. Don't return pooled arrays or dispose owners early.

For publishing versions to readers, use a snapshot store rather than racing on the struct itself:

```csharp
var snapshots = new SumTreeSnapshotStore<char>("Hello\n".ToSumTreeWithLines());

var updated = snapshots.Current.AddRange("World\n".AsMemory());
snapshots.Publish(updated);

var captured = snapshots.Current; // readers capture once, keep using it
```

Publication is atomic (acquire/release) but doesn't merge concurrent edits — multiple writers still need to coordinate the whole read/edit/publish sequence, e.g. with a lock.

## Ballpark

| Operation | SumTree\<T> | String | StringBuilder | List\<T> | ImmutableList\<T> |
|---|---|---|---|---|---|
| Concat | **O(log n)** | O(n) | Amortized O(1)\* | O(n) | O(log n) |
| Insert / Remove | **O(log n)** | O(n) | O(n) | O(n) | O(log n + k)† |
| Random access | **O(log n)** | O(1) | O(1) | O(1) | O(log n) |
| Immutable | ✓ | ✓ | ✗ | ✗ | ✓ |

\* Appends only; conversion to string is still O(n).
† k = modified leaf chunk size.

## Coordinates and conventions

- Offsets and columns count UTF-16 code units. Surrogate pairs take two offsets; columns are not glyph widths.
- Offsets are zero-based; line numbers and columns are one-based. CR, LF, and CRLF each count as one line break.

Full contracts live in [editor semantics](docs/editor-semantics.md); recent behavior changes are in the [migration notes](docs/migration.md). If you're upgrading from an older snapshot, read the migration notes first.

## License

MIT — see [LICENSE](LICENSE). Based on the rope paper by Boehm, Atkinson, and Plass; inspired by editor data structures like those in VS Code and Xi, and by Andrew Chisholm's [Rope](https://github.com/FlatlinerDOA/Rope) library.
