# Code review follow-ups

The typed-core migration was committed as a baseline before addressing these
review findings. The existing suite passes; these cases need targeted coverage.

- Unify key identity across TreeMap lookup/insertion, ordered-input validation,
  bulk deduplication, and TreeSet merge operations. `Equals` and `CompareTo == 0`
  currently disagree. Under invariant culture, strings `"a"` and `"a\0"` reproduce
  inconsistent Contains/SetEquals results and a constructor exception.
- Make TreeMap.Update always store the callback result. Its equality guard throws
  for a stored null and retains the old reference when a distinct returned object
  compares equal.
- Avoid remove-and-reinsert when TreeSet.Add encounters an existing element.
- Either implement a builder that assembles branch levels once, or correct its
  linear-construction claim: each flushed chunk currently uses a persistent join.
- Fix the pre-existing Closest exact-match behavior: keys {1, 3} produce no result
  for Closest(1), and key 1 for Closest(3), despite the documented inclusive bound.
- Revisit Extend batch heuristics and the remaining TValue IEquatable constraint.
- Decide which retired capabilities to port. Cursors, edit batching/adjustment,
  pattern search, line/column insertion, keyed-item helpers, and pooled-memory
  utilities are preserved under `../legacy/`.

No benchmark timing comparison was performed during the review.
