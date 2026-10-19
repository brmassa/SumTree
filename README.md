# SumTree

![logo](docs/logo.png)

[![Build Status](https://github.com/brmassa/SumTree/actions/workflows/continuous-integration.yml/badge.svg)](https://github.com/brmassa/SumTree/actions)
[![License](https://img.shields.io/github/license/brmassa/SumTree.svg)](https://github.com/brmassa/SumTree/blob/main/docs/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/com.BrunoMassa.SumTree.svg)](https://www.nuget.org/packages/com.BrunoMassa.SumTree)
[![downloads](https://img.shields.io/nuget/dt/com.BrunoMassa.SumTree)](https://www.nuget.org/packages/com.BrunoMassa.SumTree)
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/C0C0AE5SC)

SumTree is a rope — an immutable tree of element chunks — with a summary aggregated at every node. If you're building a text editor (or anything else that edits large sequences and asks questions about them), it's a decent fit: edits are O(log n) and share structure with the previous version instead of copying the buffer, and summaries like line counts stay up to date as you edit.

It works with any `T`, not just `char`. The design follows the classic [Ropes: an Alternative to Strings](https://www.cs.rit.edu/usr/local/pub/jeh/courses/QUARTERS/FP/Labs/CedarRope/rope-paper.pdf) paper, with summaries added on top.

## Getting started

```powershell
dotnet add package com.BrunoMassa.SumTree
```

```csharp
using SumTree;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

TextTree text = new("Hello, World!".AsMemory());
TextTree combined = text.AddRange(" How are you?".AsMemory());
Console.WriteLine(combined); // "Hello, World! How are you?"
```

The type has three parameters — element, summary, and operations — so the alias above keeps everyday code readable. The tree mostly behaves like you'd hope: `Length`, an indexer, `Slice`, `RemoveRange`, `InsertRange`, `SplitAt`, `ToArray`, plus LINQ over `IEnumerable<T>`. Construction is flexible — from a `ReadOnlyMemory<T>`, a pair of trees (`+`), an empty tree, or `SumTreeBuilder` for bulk appends.

## Summaries

This is the part a plain rope doesn't give you. The summary of any range is answered from cached node values:

```csharp
TextTree document = new(@"Line 1
Line 2
Line 3".AsMemory());

var (line, column) = document.GetLineAndColumn(10); // line 2, column 4
string? lineContent = document.GetLineContent(1);   // "Line 1"
long lineStart = document.FindLineStart(2);         // 7

TextTree modified = document.InsertRange(7, "Modified ".AsMemory());
TextSummary summary = modified.GetRangeSummary(0, 16);
```

Three summaries ship with the library:

- `TextSummary` — the ready-made composite of line and bracket metrics used above.
- `BracketBalanceSummary` — net and minimum prefix depth per bracket type, useful for catching premature closes like `)(` on a `SumTree<char, BracketBalanceSummary, BracketBalanceSummaryOperations>` via `FindPosition(summary => summary.Parentheses.MinimumPrefixBalance < 0)`; deliberately separate so you only pay for the node storage if you opt in.
- `LineNumberSummary` / `BracketCountSummary` — the individual metrics behind `TextSummary`, usable standalone.

One caveat worth knowing up front: bracket counts are raw text. Strings and comments are included, and equal open/close counts say nothing about nesting — `)(` and `([)]` both come out "balanced". Anything syntax-aware needs a real parser.

Your own metrics just need an `ISummaryOperations<T, TSummary>` implementation — static, value-type, no registration:

```csharp
public readonly struct VowelCountOperations : ISummaryOperations<char, int>
{
    public static int Identity => 0;
    public static int SummarizeElement(char element) =>
        "aeiouAEIOU".Contains(element) ? 1 : 0;
    public static int SummarizeSpan(ReadOnlySpan<char> elements)
    {
        var count = 0;
        foreach (var element in elements)
        {
            if ("aeiouAEIOU".Contains(element)) { count++; }
        }
        return count;
    }
    public static int Combine(in int left, in int right) => left + right;
}

var text = new SumTree<char, int, VowelCountOperations>("Hello World".AsMemory());
int vowels = text.Summary; // 3
```

The one hard requirement: combination must be associative and agree with element-by-element composition, whatever the chunk boundaries happen to be. Implementations should be pure and thread-safe.

## How it works

The tree uses immutable B+ nodes (up to eight children inline, uniform leaf depth) that split and join by copying boundary paths and sharing the rest. Leaves hold up to 1024 elements, and small neighboring leaves get coalesced into a 512-element buffer.

A few things to watch for:

- Index and range arguments are validated strictly — no clamping.
- `GetEnumerator()` returns a `ref struct`, so plain `foreach` works (allocation-free), but if you need an `IEnumerator<T>` — say, across an `await` — go through `((IEnumerable<T>)tree).GetEnumerator()`.
- Seek predicates (via `Func<TSummary, bool>` or the allocation-free `ISummarySeek<TSummary>`) must be monotonic: once a prefix reaches the target, every longer prefix must too. Results are inclusive indices; a miss returns -1.
- The built-in summaries use 32-bit counters that throw `OverflowException` instead of wrapping. For very large documents, bring wider counters of your own. `BracketBalanceSummary` is the exception, with checked 64-bit math.

## Snapshots and threading

The trees are immutable, so old versions stay valid as you edit. Two things to keep straight:

- Constructors and insertions copy mutable input buffers (string-backed memory is shared). Copies are shallow — elements and summaries must stay immutable if versions are shared across threads.
- `FromBorrowedMemory` skips the copy. In exchange, you guarantee the input stays valid and unchanged until every derived snapshot is gone. Don't return pooled arrays or dispose owners early.

For publishing versions to readers, use a snapshot store rather than racing on the struct itself:

```csharp
var snapshots = new SumTreeSnapshotStore<char, TextSummary, TextSummaryOperations>(
    new TextTree("Hello\n".AsMemory()));

var updated = snapshots.Current.AddRange("World\n".AsMemory());
snapshots.Publish(updated);

var captured = snapshots.Current; // readers capture once, keep using it
```

Publication is atomic (acquire/release) but doesn't merge concurrent edits — multiple writers still need to coordinate the whole read/edit/publish sequence, e.g. with a lock.

## Ballpark

| Operation | SumTree | String | StringBuilder | List\<T> | ImmutableList\<T> |
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

Full contracts live in [editor semantics](docs/editor-semantics.md); recent behavior changes are in the [changelog](docs/CHANGELOG.md). If you're upgrading from an older snapshot, read the changelog first.

## License

MIT — see [LICENSE](docs/LICENSE). Based on the rope paper by Boehm, Atkinson, and Plass; inspired by editor data structures like those in VS Code and Xi, and by Andrew Chisholm's [Rope](https://github.com/FlatlinerDOA/Rope) library.
