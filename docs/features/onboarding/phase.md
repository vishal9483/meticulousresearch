# Phase — First-Run Onboarding

**SPEC:** §3.8. **Milestone:** M5. **Depends on:** settings-secure-key, projects-crud

## Goal
Guide a new user, on first launch, through a **branded** flow to a working state: welcome +
privacy, API key entry + Test key, defaults (model/theme/data dir), an optional populated sample
project, and finish onto the Projects home with hints (SPEC §3.8, §9.1(1)). Skippable at any
step and re-runnable from Settings.

## Deliverables
1. **`IOnboardingState`** — a persisted "completed" flag (via `ISettingsService`) plus current
   step; drives whether onboarding runs at launch and supports re-run.
2. **Onboarding wizard VM + views** — stepper for Welcome → API key → Defaults → Sample project
   → Done, each a styled page (design-system tokens), with Next/Back/Skip.
3. **API key step** — reuses `ISecureKeyStore` + `IKeyTester` (from settings-secure-key): masked
   entry, Test key (mocked in tests) showing success + model list or an actionable error; saves
   the key on continue.
4. **Defaults step** — pre-filled sensible defaults (Opus 5, System theme, default data dir),
   persisted via `ISettingsService`; data-directory writability validated (reuse settings
   validation).
5. **Sample project builder** — an `ISampleProjectFactory` that, via `IProjectService`, creates a
   project with a couple of bundled resources and one example "Market Research Report" artifact,
   entirely from bundled content (no network / no key required).
6. **Finish** — mark complete, navigate to Projects home, and show first-run hints on primary
   actions.
7. **Re-run entry point** — a "Re-run onboarding" action in Settings that clears the step and
   relaunches the wizard.

## Suggested design
- Keep step logic (validation gates, can-advance, skip) in the wizard VM so it is `@unit`-driven;
  views are thin, proven by `@ui` (re-run, welcome text).
- Do not reimplement key handling — delegate to `ISecureKeyStore`/`IKeyTester`; assert the
  "not in plaintext" property here too since onboarding is a fresh user's first key entry.
- Sample content ships as bundled files/strings so the sample artifact/resources are created
  deterministically and offline (proven by the no-key scenario) — the example report is static
  bundled text, not a live generation.
- Advancing past the key step requires a validated key **or** an explicit skip; make that gate
  explicit and tested.

## Test-first order
1. `@unit` first-run trigger + completed-flag tests → `IOnboardingState` + launch wiring.
2. `@unit` key-step tests (valid test+save, invalid blocks, not-in-plaintext) → reuse
   secure-key/key-tester in the wizard VM.
3. `@unit` defaults tests (pre-fill + persist) → defaults step VM over `ISettingsService`.
4. `@unit` sample-project tests (opt-in populates, decline creates nothing, offline no-key) →
   `ISampleProjectFactory`.
5. `@unit` finish/skip tests (mark complete, land on home, hints) → finish wiring.
6. `@ui` re-run-from-Settings + welcome/privacy tests, `@manual` branded-flow checklist → views.

## Definition of done
- Onboarding runs once on first launch and never again after completion; skippable at any step.
- Key can be tested and is stored securely (provably not in plaintext); invalid keys give an
  actionable error and block advance until validated or skipped.
- Defaults pre-fill and persist; opting into the sample project creates a couple of resources +
  an example Market Research Report artifact, offline and without a key.
- Finishing lands on Projects home with hints; re-run from Settings restarts the flow.

## Notes for later features
- `v1-acceptance` (§9.1(1)) exercises this end-to-end from a signed installer to a branded first
  run; keep the completed-flag key stable so a clean machine triggers it.
- `about-screen` and `empty-loading-error-states` share the styled component kit used here.
- The sample project is a good default demonstration for `deliverable-templates`/`branded-export`
  reviewers — keep its example report representative of the Market Research Report template shape.
