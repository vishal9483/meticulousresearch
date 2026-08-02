# Testing Strategy

This document defines how tests are written, tagged, and automated across MeticulousResearch
Desktop. Every feature's `tests.md` follows these conventions. The goal is **accurate,
test-driven development**: tests are written from the **user's perspective**, first, and drive
the implementation.

## 1. Test format — Gherkin

All scenarios use **Given / When / Then** (BDD). This is framework-agnostic and maps to
[Reqnroll](https://reqnroll.net/) (the maintained SpecFlow successor) for .NET when automated.

```gherkin
@unit
Scenario: Renaming a project updates its display name
  Given a project named "Semiconductors 2026"
  When I rename it to "Semiconductors 2027"
  Then the project list shows "Semiconductors 2027"
  And the project's updated_at timestamp changes
```

Conventions:
- **User perspective, not implementation.** Say "I rename the project," not "call
  `ProjectService.Rename()`." The `@unit` tag decides how it's *driven*, but the sentence stays
  behavioral.
- Use `Scenario Outline` + `Examples` for data-driven cases (multiple file types, model tiers).
- Use `Background` for shared setup within a feature.
- Keep one observable outcome family per scenario; prefer several small scenarios over one long one.

## 2. Test tags — how each scenario is verified

Every scenario carries exactly one primary tag:

| Tag | Meaning | Tooling | Speed |
|-----|---------|---------|-------|
| `@unit` | Logic verifiable through a Core service / view-model without the real window. True red-green TDD. | **xUnit** against the Core class library & view-models | fast |
| `@ui` | Requires driving the real WPF window (click, type, observe rendered state). | **FlaUI** (UIA3) end-to-end against the built app | slow |
| `@manual` | Subjective/visual (branding, "looks professional", motion) — verified by a human against a checklist. | Human + screenshot | n/a |

Secondary tags allowed: `@integration` (touches SQLite/filesystem/sidecar), `@slow`,
`@requires-network` (real API — normally mocked; only v1-acceptance uses live), `@requires-key`.

**TDD implication:** Prefer to push logic into Core / view-models so it can be `@unit`-tested.
`@ui` tests exist to prove the wiring; they are not where business rules are verified.

## 3. Test project layout (created in M0)

```
src/
  MeticulousResearch.Core/            <- domain, services, versioning, cost, export
  MeticulousResearch.App/             <- WPF, MVVM view-models & views
tests/
  MeticulousResearch.Core.Tests/      <- xUnit  (@unit, @integration)
  MeticulousResearch.App.Tests/       <- xUnit on view-models (@unit)
  MeticulousResearch.UiTests/         <- FlaUI  (@ui)
  MeticulousResearch.Specs/           <- Reqnroll feature files (optional binding of .md scenarios)
```

`MeticulousResearch.App.Tests` covers view-models with a **mocked `IChatService`** so streaming,
model selection, and turn actions are `@unit`-testable without the network.

## 4. Test doubles & determinism

- **AI calls** are mocked by default via a `FakeChatService : IChatService` that replays scripted
  token streams, usage numbers, and error codes (429, 5xx) so backoff/streaming/cost are
  deterministic. Only `@requires-network` scenarios hit the real API.
- **Clock** is injected (`IClock`) so timestamps, backoff jitter, and "today/this week" cost
  windows are testable.
- **Filesystem** uses a temp project data dir per test; no test touches `%LOCALAPPDATA%` real data.
- **Token estimation** must be deterministic for a given input.

## 5. Definition of Done for a feature

A feature is done when:
1. All `@unit` scenarios in its `tests.md` are implemented and green.
2. All `@ui` scenarios are implemented and green (or explicitly deferred with reason in the PR).
3. `@manual` scenarios have a checked-off checklist in the PR description.
4. No regression in previously-green tests.
5. Code matches the surrounding style; no unstyled/placeholder UI (SPEC §3.7).

## 6. Traceability

Each `tests.md` begins with a **Traceability** block linking scenarios back to SPEC sections and
to the acceptance criteria in SPEC §9.1, so coverage of the v1.0 bar is auditable.
