# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-10-19

### Added

- `TreeMap.Snapshot()` returns an O(1) shared snapshot unaffected by later mutations
- `SumTreeBuilder<T, TSummary, TOps>` batches element appends into full chunks so bulk construction is linear instead of copying the boundary leaf on every `Add`
- Typed `SumTree<T, TSummary, TOps>` implements `IEquatable<T>` with `==`/`!=`, comparing elements regardless of leaf boundaries
- `TreeMap.TryRemove` reports whether the key existed
- `LineNumberSummary.SummarizeSpan` and `BracketCountSummary.SummarizeSpan` summarize spans directly, without a dimension object

### Changed

- **Breaking:** the `TreeMap` indexer now throws `KeyNotFoundException` for missing keys instead of returning `default`; use `Get` or `TryGetValue` for the old behavior
- **Breaking:** `TreeMap.Insert` now returns `bool` reporting whether a new key was inserted (previously returned nothing)
- **Breaking:** `TreeMap.Update` takes `Func<TValue, TValue>` (applied to the stored value type) instead of `Func<TValue, TResult>`
- **Breaking:** `TreeMap.First`, `TreeMap.Last` and `TreeMap.Closest` now return `Maybe<...>`, consistent with `TreeSet`, instead of nullable `KeyValuePair`s
- **Breaking:** the `TreeMap` constructor from ordered entries now throws `ArgumentException` when the input is unsorted or contains duplicate keys
- `TreeMap` is now built on the typed `SumTree<MapEntry, MapKey, MapKeySummaryOperations>`; key lookups and seeks descend the node summaries in O(log n) instead of probing with a binary search over indexed reads
- `TreeSet` set operations (`Except`, `Intersect`, `Union`, `SymmetricExcept`, subset/superset/overlap checks) are linear-time merge walks instead of repeated lookups
- Faster editing for large texts: `FindPosition` descends the cached summaries in O(log n) (the predicate must be monotonic), summaries combine without reflection, long buffers are split into 1 KB leaves, and small leaves merge into their neighbours so character-by-character edits keep leaves large

### Removed

- **Breaking:** the runtime-dimension tree and everything that only served it: `SumTree<T>` (single type parameter), `WithDimension`/`GetSummary(dimension)`/`HasDimension`, `ISummaryDimension`, `SummaryDimensionBase`, the dimension classes (`LineNumberDimension`, `BracketCountDimension`, `BracketBalanceDimension`), cursors (`ICursor`, `SumTreeCursor`, cursor extensions), the non-generic `SumTreeSnapshotStore`, `IKeyedItem`, `Edit`, and the helper extensions (`ToSumTree`, `ToSumTreeWithLines`, `ToSumTreeWithLinesAndBrackets`, `FindNthOpenBracket`, `InsertAtLineColumn`, `HaveEqualBracketCounts`, `AreBracketsBalanced`, `WithDimensions`, and the rest of `SumTreeHelpers`/`SumTreeExtensions`). The typed `SumTree<T, TSummary, TOps>` is now the only tree; construct it with `new SumTree<T, TSummary, TOps>(memory)`
- **Breaking:** the public `MapKeyDimension<TKey, TValue>` type; `TreeMap` now carries key summaries natively through the typed tree

### Fixed

- `IterateFrom` is lazy again: it no longer copies the whole suffix before yielding the first item

## [1.1.0] - 2025-10-13

- automatic nuget package publish

## [1.0.2] - 2025-08-17

## [1.0.1] - 2025-08-16

- slightly changed in the README
- Fix escaping the CHANGELOG when creating releases

## [0.1.0-19] - 2025-07-10

- Added cursor, TreeMap and TreeSet

## [0.1.0-15] - 2025-06-26

- Fixes in the Github Actions

## [1.0.0] - 2025-06-25

- Initial release of SumTree library

[Unreleased]: https://github.com/brmassa/SumTree/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/brmassa/SumTree/compare/v1.1.0...v2.0.0
[1.1.0]: https://github.com/brmassa/SumTree/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/brmassa/SumTree/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/brmassa/SumTree/compare/v1.0.0...v1.0.1
