# Phase — Usage CSV Export

**SPEC:** §3.6. **Milestone:** M4. **Depends on:** cost-tracking

## Goal
Export a project's cost/usage as a **per-turn CSV** for reporting and reconciliation (SPEC §3.6).
One row per completed turn (conversation turns and artifact generations), with cost **computed
from current prices** via `ICostService`.

## Deliverables
1. **`IUsageCsvExporter`** in `MeticulousResearch.Core`:
   - `Export(projectId, destination)` → writes a CSV file.
   - `Render(projectId)` → returns the CSV text (for preview/tests without disk).
2. **Row shape** — header + one row per completed turn with columns:
   `timestamp, source, model, tokens_in, tokens_out, cost_usd`
   (source ∈ `conversation` | `artifact`). Extend with cache-token columns if present in the
   data, but keep the core set above.
3. **Cost column** — obtained from `ICostService` per-turn priced records, i.e. computed from
   stored tokens × current catalog prices (a price update reprices the export).
4. **Deterministic ordering & formatting** — ascending timestamp; RFC-4180 quoting/escaping;
   invariant-culture number formatting; stable newline.
5. **Dashboard hook** — an "Export usage CSV" action on the consolidated cost panel.

## Suggested design
- Consume the priced per-turn record enumerable exposed by `cost-tracking` — do **not**
  recompute cost here; this feature is a serializer over `ICostService` output.
- Use invariant culture and a fixed number format for `cost_usd` and token columns so output is
  culture-independent and deterministic.
- Escape any field containing a comma, quote, or newline per RFC 4180; round-trip in tests.
- No network; pure filesystem write. Inject the clock only insofar as `cost-tracking` needs it.

## Test-first order
1. Row-shape + one-row-per-turn `@unit @integration` tests → record → CSV mapping.
2. Source-column + computed-cost `@unit @integration` tests → wire `ICostService` records.
3. Repricing `@unit @integration` test → confirm cost follows current prices, tokens unchanged.
4. Determinism/ordering/escaping/empty `@unit @integration` tests → formatter.
5. Offline `@unit @integration` test → assert no network.
6. `@ui` test (dashboard export action) → view wiring.

## Definition of done
- All `@unit @integration` scenarios green; repeat export is byte-identical; no network.
- One row per completed turn with the specified columns and a computed cost that reprices with
  the catalog (§9.1(7)).
- Escaping round-trips; empty project yields header-only CSV.

## Notes for later features
- Keep this separate from `branded-export`'s themed document pipeline — usage CSV is raw data,
  not a branded deliverable.
- If a future report needs monthly rollups, add them as an aggregate view over the same
  `ICostService` records rather than duplicating cost logic here.
