# CLAUDE.md — MeticulousResearch Desktop

A .NET 8 WPF desktop app for analysts to generate research documents with Claude (Agent SDK /
direct API). Built **TDD-by-agent** from pre-written specs.

**Sources of truth (read before any feature work):**
- [`SPEC.md`](SPEC.md) — product & architecture specification (the cited sections are authoritative).
- [`docs/README.md`](docs/README.md) — feature index + dependency order.
- [`docs/TESTING-STRATEGY.md`](docs/TESTING-STRATEGY.md) — Gherkin conventions, tags, tooling.
- `docs/features/<slug>/{phase.md,tests.md}` — the spec + Gherkin for each feature.

## Build & test

```bash
dotnet build MeticulousResearch.sln -c Debug
# Headless CI gate (the merge gate — must be green):
dotnet test MeticulousResearch.sln -c Debug --filter "Category!=ui&Category!=manual"
```

Only .NET SDK 7 and 9 are installed; `global.json` rolls forward and builds `net8.0` fine.

Solution = `MeticulousResearch.sln`, 6 projects:
- `src/MeticulousResearch.Core` (net8.0) — domain, services, persistence, cost, export.
- `src/MeticulousResearch.App` (net8.0-windows) — WPF + CommunityToolkit.Mvvm view-models/views.
- `tests/MeticulousResearch.Core.Tests` — xUnit (@unit, @integration).
- `tests/MeticulousResearch.App.Tests` (net8.0-windows) — xUnit on view-models.
- `tests/MeticulousResearch.UiTests` — FlaUI (@ui, compile-only headless).
- `tests/MeticulousResearch.TestSupport` — shared fakes (`FakeClock`, `FakeEnvironment`).

## Trait mapping (easy to get wrong — breaks the gate)

The CI filter is `Category!=ui&Category!=manual`. Map each Gherkin scenario's primary tag:
- `@unit` → **no** `Category` trait (runs in the gate).
- `@ui` → `[Trait("Category","ui")]` in UiTests (must **compile** headless; need not run).
- `@manual` → `[Trait("Category","manual")]`, a skipped test with a checklist comment.
- Secondary tags (`@integration`, `@requires-key`, …) → extra `[Trait]`.
- `Scenario Outline` → `[Theory]` + one `[InlineData]` per `Examples` row (no missing rows).

## TDD-by-agent workflow

- Every feature has hand-written Gherkin in `docs/features/<slug>/`. Translate **every** scenario
  faithfully to xUnit — never soften, skip, tautologize, or reword an assertion to pass.
- Topology: an **implementer** codes Gherkin→green, then an independent **adversarial reviewer**
  gates the merge (repair loop capped at 3). Agent definitions live in `.claude/agents/`.
- Orchestrator: `.claude/workflows/build-milestone.js` walks the dependency DAG per milestone,
  merging each APPROVEd feature into a moving integration branch. **Milestone-gated**: get human
  sign-off between milestones.
- **Contract features** (`data-store-migrations`, `ai-gateway`, `model-selector`,
  `design-system-theming`) own cross-cutting contracts downstream features consume — extra scrutiny.
- Credentials resolve **env-first**: `ANTHROPIC_API_KEY` / `ANTHROPIC_BASE_URL` win over persisted
  settings; an env key is never written to SQLite/settings/command line.

## Repo conventions

- File-scoped namespaces; `Nullable` enabled; XML-doc on public contracts.
- `Directory.Build.props` centralizes `Nullable`/`ImplicitUsings` — don't re-add them per csproj.
  Add `PackageReference`s to the project that needs them.
- Push logic into Core/view-models so it's `@unit`-testable without a window; constructor-inject VMs.
- A behavior scoped to a **downstream** feature may be a loud seam (`throw NotSupportedException`
  naming the owner) rather than a fake-pass.
- Commit trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Work on local feature branches (`feat/<slug>`); nothing is pushed unless asked.

## Never (from a feature)

Do **not** modify `docs/`, `global.json`, `Directory.Build.props`, or the CI filter. Do not touch
other feature branches.
