# Phase — Artifact Diff

**SPEC:** §3.4. **Milestone:** M3. **Depends on:** artifact-versioning

## Goal
Let the analyst **compare any two versions** of an artifact, side-by-side or inline, so an edit or
regeneration can be reviewed before it is kept. This is a read-only view over the version history
owned by `artifact-versioning`; it computes and presents differences, it does not create versions.

## Deliverables
1. **Diff engine** in Core — `Diff(baseVersion, compareVersion)` returning a structured result
   (ordered hunks of unchanged / added / removed regions). Deterministic for given inputs.
2. **Format-aware diffing**:
   - doc/text/code/diagram → line-based text diff over the version content (Mermaid diffs as text).
   - table (CSV) → row/cell-aware diff (added/removed rows, changed cells).
3. **Any-pair support** — diff any two versions regardless of adjacency; direction matters
   (base → compare) so removals/additions are labeled from the base's perspective.
4. **Views/VMs**: diff mode in the artifact editor with two version pickers, side-by-side (two
   panes) and inline (merged) presentations, highlighted changed regions, and sensible defaults
   (previous vs. current). Disabled state when only one version exists.

## Suggested design
- Use a standard LCS/Myers line-diff for text; keep the engine in Core so it is `@unit`-testable
  independent of the WPF view.
- For tables, parse both versions' CSV into rows keyed by position (or a stable key column if
  present) and diff structurally, then map results to the grid view.
- Return a view-model-friendly diff model (list of segments with a change kind) so both
  side-by-side and inline renderers consume the same computation.
- Diff is pure/read-only: it never mutates versions or sets current.

## Test-first order
1. Text diff `@unit` tests (changed/identical/additive/direction) → line diff engine.
2. Any-pair `@unit` tests → arbitrary base/compare selection.
3. Format-aware `@unit` tests (table rows/cells, diagram source) → per-format diffing.
4. `@ui` tests (side-by-side, inline, default pickers, single-version disabled) → editor diff mode.

## Definition of done
- Diffing any two versions reports adds/removes correctly and is direction-aware.
- Identical content reports no changes; table and diagram artifacts diff meaningfully.
- Editor offers side-by-side and inline modes with highlighted changes and sensible default
  version selection; diff is disabled (with a hint) when only one version exists.

## Notes for later features
- `edit-with-claude` naturally pairs with diff: after a Claude edit produces a new version, the
  user compares it against the prior one before keeping/reverting — reuse this diff view.
- `report-composition` may later diff whole compositions; the per-artifact engine here is the base.
