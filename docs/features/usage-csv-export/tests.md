# Tests — Usage CSV Export

**SPEC:** §3.6 (export project cost/usage as CSV, per-turn rows). **Milestone:** M4.
**Depends on:** cost-tracking (priced per-turn records)

## Traceability
- §3.6 project cost/usage exportable as CSV, per-turn rows → CSV row scenarios.
- §3.6 cost computed from current prices (tokens are ground truth) → Computed-cost column scenarios.
- §9.1(7) export usage CSV → covered here.

> **Determinism & offline (TESTING-STRATEGY §4):** CSV export is `@unit`/`@integration`, makes
> no network call, and produces identical output for identical input under a fixed clock and
> fixed price table. Cost columns come from `ICostService` (owned by `cost-tracking`).

---

```gherkin
Feature: Usage CSV export
  As a research manager
  I want to export a project's usage as a per-turn CSV
  So that I can report and reconcile spend outside the app
```

## Background

```gherkin
Background:
  Given a fixed price table in USD per million tokens:
    | model         | input | output |
    | claude-opus-5 | 5     | 25     |
  And a project "EV Market 2026" with these completed turns:
    | when                | source        | model         | tokens_in | tokens_out |
    | 2026-08-03T09:00:00 | conversation  | claude-opus-5 | 1000000   | 100000     |
    | 2026-08-03T10:00:00 | artifact      | claude-opus-5 | 500000    | 50000      |
```

### Per-turn rows

```gherkin
@unit @integration
Scenario: The CSV has one row per completed turn
  Given the project "EV Market 2026"
  When I export its usage as CSV
  Then the CSV has 2 data rows
  And a header row

@unit @integration
Scenario: Each row carries the per-turn usage fields
  Given the project "EV Market 2026"
  When I export its usage as CSV
  Then each row includes: timestamp, source, model, tokens_in, tokens_out, cost_usd

@unit @integration
Scenario: The source column distinguishes conversations from artifact generations
  Given the project "EV Market 2026"
  When I export its usage as CSV
  Then the first row's source is "conversation"
  And the second row's source is "artifact"
```

### Computed cost column

```gherkin
@unit @integration
Scenario: The cost column is computed from tokens and current prices
  Given the project "EV Market 2026"
  When I export its usage as CSV
  Then the first row's cost_usd is 7.50
  And the second row's cost_usd is 3.75

@unit @integration
Scenario: A price update changes the exported cost from the same tokens
  Given the price for "claude-opus-5" input changes to 10 per MTok
  When I export the project's usage as CSV
  Then the first row's cost_usd reflects the new price
  And the token columns are unchanged
```

### Well-formed, deterministic output

```gherkin
@unit @integration
Scenario: Rows are ordered deterministically by timestamp
  Given the project "EV Market 2026"
  When I export its usage as CSV
  Then rows appear in ascending timestamp order

@unit @integration
Scenario: The same project exports byte-identical CSV on repeat
  Given the project "EV Market 2026" and a fixed clock and price table
  When I export its usage as CSV twice
  Then the two CSV files are identical

@unit @integration
Scenario: Fields needing escaping are quoted per CSV rules
  Given a turn whose model label contains a comma
  When I export the project's usage as CSV
  Then that field is quoted and the CSV parses back to the original values

@unit @integration
Scenario: A project with no completed turns exports a header-only CSV
  Given a project with no completed turns
  When I export its usage as CSV
  Then the CSV has a header row and no data rows

@unit @integration
Scenario: CSV export makes no network calls
  Given the project "EV Market 2026"
  When I export its usage as CSV
  Then no network request is made
```

### UI

```gherkin
@ui
Scenario: Usage CSV is exportable from the project dashboard cost panel
  Given the project dashboard for "EV Market 2026" is open
  When I choose "Export usage CSV" and pick a destination
  Then a CSV file is written to that destination
  And a confirmation is shown
```
