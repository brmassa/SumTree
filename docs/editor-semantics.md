# Editor summaries and positions

SumTree stores elements plus associative summaries. Which summaries to keep, and how to navigate, is the editor's call — the library doesn't impose a policy.

## Picking summaries

| Summary | Useful for | Limits |
|---|---|---|
| `LineNumberSummary` | Line breaks, last-line length, line/column navigation | UTF-16 units; signed 32-bit counters |
| `BracketCountSummary` | Raw occurrences, Nth-bracket queries | Equal counts say nothing about order or nesting |
| `BracketBalanceSummary` | Net depth, lowest prefix depth, first premature close per type | Raw text; can't detect crossing between types |
| `TextSummary` | Example composite of lines + raw bracket counts | No prefix balances, no syntax awareness |

These are optional building blocks, not a required stack. Most language tooling will want its own composite summary or separate syntax data — extra summaries mean bigger nodes and more combine work, which is why `BracketBalanceSummary` isn't folded into `TextSummary`.

### Count equality, prefix balance, nesting

`HasEqualCounts` (and its alias `IsBalanced`) means open and close counts match for each of `()`, `[]`, `{}`. `GetPrefixSummary(index).Brackets.HasEqualCounts` checks the half-open prefix `[0, index)`.

`DelimiterBalanceSummary` stores a net depth and the minimum depth over all prefixes, including the empty one. Composition of A followed by B:

```
balance = A.balance + B.balance
minimum = min(A.minimum, A.balance + B.minimum)
```

Associative, identity `(0, 0)`. A single pair is balanced when the final depth is zero and the minimum never dipped below it.

| Text | Equal raw counts | Balanced prefixes per type | Valid mixed-type nesting |
|---|---|---|---|
| `({[]})` | yes | yes | yes |
| `)(` | yes | no | no |
| `([)]` | yes | yes | no |

`BracketBalanceSummary.HasBalancedPrefixes` checks the three types independently. Everything is scanned, quotes and comments included — neither raw summary establishes syntactic correctness. Real mixed-type nesting needs unmatched-type tracking across boundaries or a stack/parser; counts and minima can't encode it.

```csharp
using SumTree;

var text = new SumTree<char, BracketBalanceSummary, BracketBalanceSummaryOperations>(
    ")(".AsMemory());
bool prefixesBalanced = text.Summary.HasBalancedPrefixes; // false
long firstPrematureClose = text.FindPosition(
    summary => summary.Parentheses.MinimumPrefixBalance < 0); // 0
```

That predicate is monotonic: once a prefix drops below zero, every longer prefix stays there. Seeks like `Balance == 0` or `Balance >= target` are not monotonic — later closes can reduce net balance — so they're invalid in general.

### Folding and indentation

Folds depend on which tokens are delimiters, and that depends on quotes, comments, interpolated strings, preprocessor state, and incomplete syntax. Keep lexer/parser state per snapshot and derive folds from tokens or syntax nodes. Indentation similarly depends on tab settings, blank lines, and language rules — the library doesn't pick any of it.

One rule for custom summaries: they must combine correctly across *arbitrary* partitions. If a leaf's tokenization depends on preceding text, counting its tokens independently isn't enough — track lexer state transitions or re-analyze affected regions until state stabilizes. Keep analysis tied to the snapshot it describes, so background work can't land on an unrelated version.

## Text coordinates and newlines

- An element of the text tree is one UTF-16 code unit. Offsets, lengths, and columns count units: a surrogate pair is two offsets, combining marks take their own. Columns are not glyph counts or screen widths, and tabs don't expand.
- Leaves can split between surrogate halves or between CR and LF; editors must enforce their own caret boundaries. UTF-8 bytes, scalars, graphemes, and display cells need extra metrics or traversal.
- Offsets are zero-based, ranges half-open, lines and columns one-based. `GetLineAndColumn(index)` summarizes `[0, index)`.
- CR, LF, and CRLF each count as one line break, and CRLF stays one break even when divided between leaves. Other Unicode line separators are ordinary text in the supplied summary.
- The point between CR and LF and the point after LF both map to next-line column one. The map is deliberately not bijective — editors can prevent carets inside CRLF or normalize those offsets.
- `FindLineStart` returns the position after the terminator, `-1` if the line is missing. `GetLineContent` omits the terminator and returns `null` when missing.
- Empty text has one empty line; a trailing terminator creates a final empty line.

The text helpers target the `TextSummary` composite; other summaries can build their own mapping from `GetPrefixSummary`, `GetRangeSummary`, and monotonic seeks. Range queries combine cached subtrees and scan boundary spans without allocating. Note that `FindPosition` returns the inclusive element index that reaches the target — a different thing from the endpoint `FindLineStart` returns.

## Limits and arithmetic

`LineNumberSummary`, `BracketCountSummary`, and `TextSummary` use signed 32-bit counters and throw `OverflowException` instead of wrapping; raw bracket totals throw past `int.MaxValue` too. A line summary's character count caps its text at `int.MaxValue` UTF-16 units, and the one-based line/column helpers throw when their result doesn't fit. `BracketBalanceSummary` uses checked 64-bit values — its span scanner can't overflow, since span lengths are `int`.

The typed tree stores length as a checked `long` and leaf count as a checked `int`; `Count`, arrays, and strings require `int`-representable sizes, and `Count` saturates at `int.MaxValue` — use `Length` for the logical length. These are representation limits, not a promise that such sizes are allocatable. Custom operations choose their own overflow policy; just keep values where identity and associativity hold. Checked exceptions leave older snapshots intact. Manually constructed summaries must describe real segments — constructors don't validate arbitrary counter combinations or restore discarded boundary info.

## Ownership, publication, and reader state

Trees are persistent: edits build new nodes and share unchanged subtrees. Readers can use a safely published snapshot freely as long as elements, summaries, configuration, and backing storage are immutable. Mutable array inputs get copied, but shallowly.

`FromBorrowedMemory` shifts the burden to you: keep the storage valid and unchanged until every derived snapshot is gone. That rules out returning pooled arrays or disposing owners early.

Use `SumTreeSnapshotStore` or external synchronization to publish. Its acquire/release publication doesn't combine concurrent edits — with multiple writers, lock the whole read/edit/publish sequence or the last publish can silently drop someone else's edit.

Cursors and enumerators belong to a single reader. Typed `foreach` and `GetChunks` use stack-only span cursors — no allocations, but they can't be held across `await` or `yield`; get an `IEnumerator<T>` through `IEnumerable<T>` when you need one (that adapter allocates). Sharing immutable nodes makes the structure pleasant to use concurrently; it is not a mutable concurrent collection or a collaborative editing protocol.
