# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased][]

- Faster editing for large texts: `FindPosition` descends the cached summaries in O(log n) (the predicate must be monotonic), summaries combine without reflection, long buffers are split into 1 KB leaves, and small leaves merge into their neighbours so character-by-character edits keep leaves large
- Fix summaries of trees concatenated with an element that had no dimensions (such as `Add`), which were counted as empty

## v[1.1.0][] 2025-10-13

- automatic nuget package publish

## v[1.0.2][] 2025-08-17

## v[1.0.1][] 2025-08-16

- slightly changed in the README
- Fix escaping the CHANGELOG when creating releases

## v[0.1.0-19][] 2025-07-10

- Added cursor, TreeMap and TreeSet

## v[0.1.0-15][] 2025-06-26

- Fixes in the Github Actions

## v[1.0.0][] 2025-06-25

- Initial release of SumTree library

[1.1.0]: https://github.com/brmassa/SumTree/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/brmassa/SumTree/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/brmassa/SumTree/compare/v1.0.0...v1.0.1
[Unreleased]: https://github.com/brmassa/SumTree/compare/v1.1.0...HEAD
