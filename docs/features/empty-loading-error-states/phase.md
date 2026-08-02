# Phase — Empty, Loading & Error States

**SPEC:** §3.7. **Milestone:** M5. **Depends on:** design-system-theming

## Goal
Give every list/view a designed **empty**, **loading**, and **error** state so the app never
shows a blank pane, a placeholder screen, or a raw stack trace (SPEC §3.7, §9.1(10)). Owns the
**view-state pattern** and the **shared state components** that all other views reuse.

## Deliverables
1. **View-state model** — a shared `ViewState` (`Loading | Empty | Content | Error`) and a base
   view-model (or mixin) exposing the current state plus an actionable command for the
   empty/error CTA. Views bind to state, not to raw collections.
2. **Shared controls** (styled via design-system-theming tokens):
   - `EmptyState` — icon + message + call-to-action button.
   - `SkeletonLoader` — shimmer placeholders for list rows and editor blocks.
   - `ErrorState` — human-readable message + recovery-action button.
3. **Error-mapping** — an `IUserErrorMapper` that turns known failures (missing key, offline,
   rate-limited, extraction-failed) and unexpected exceptions into `{message, recovery-action}`,
   logging the raw detail instead of showing it.
4. **Adoption** — Projects home, Resources, Conversations, Artifacts (and the editor/preview
   panes) render the three states via these controls.

## Suggested design
- Keep the state machine in the view-model so `@unit` tests assert Loading → Content / Empty /
  Error transitions without the window.
- The error mapper is a pure function over a failure classification (reuse the same error
  taxonomy `ai-gateway`/`settings-secure-key` already surface: 401→missing/invalid key,
  network→offline, 429→rate limited); recovery actions are commands passed in by the host view.
- Skeletons are purely visual (`@manual`/`@ui`); the *presence* of a non-blank loading state is
  `@unit`-observable via `ViewState.Loading`.
- Never `ToString()` an exception into the UI — always route through the mapper.

## Test-first order
1. `@unit` state-transition tests (Empty/Loading/Content) → view-state model + base VM.
2. `@unit` error-mapping tests (each failure → message + recovery; unexpected → generic + log) →
   `IUserErrorMapper`.
3. `@unit` recovery-re-runs-operation test → wire recovery command.
4. `@ui` empty/skeleton/error rendering tests → shared controls + adoption in primary views.
5. `@manual` skeleton-shape + cross-view-consistency checklist → visual pass in the PR.

## Definition of done
- Every primary list has a designed empty state with a working CTA (no blank screen).
- Async views show skeletons while loading; state transitions are `@unit`-proven.
- Every known failure maps to an actionable message + recovery; unexpected errors show a generic
  message and log the detail — no raw stack trace anywhere.

## Notes for later features
- `onboarding` and `about-screen` reuse these states; `accessibility` adds screen-reader labels
  to the empty/error CTAs.
- New views added by any feature MUST use the `ViewState` pattern and shared controls, not
  bespoke blank/loading/error UI.
- The error taxonomy here should stay aligned with what `rate-limit-backoff` surfaces for its
  "retrying…" state.
