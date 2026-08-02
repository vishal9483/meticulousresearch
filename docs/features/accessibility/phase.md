# Phase — Accessibility

**SPEC:** §8 (+ §3.7 focus visible). **Milestone:** M5. **Depends on:** design-system-theming

## Goal
Make the app **keyboard-navigable**, **screen-reader friendly**, and **WCAG-AA legible in both
themes** (SPEC §8): every primary control has an accessible name, tab order is logical, focus is
visible, dialogs trap and restore focus, and text/controls meet AA contrast. This is a
cross-cutting pass over the shell and all M0–M4 views, plus guardrails to keep it that way.

## Deliverables
1. **Automation-label pass** — set `AutomationProperties.Name` (and `HelpText`/`LabeledBy` where
   apt) on every primary control across screens; icon-only buttons get descriptive names;
   inputs are label-associated.
2. **Keyboard model** — verify/repair `TabIndex` ordering per screen, `IsTabStop` on
   interactive controls only, and focus-trap + restore on modal dialogs.
3. **Focus visibility** — ensure a visible focus adorner/`FocusVisualStyle` from the design
   system that reads in both themes.
4. **Contrast guarantees** — extend the design-token contrast checks to cover interactive
   controls (button fills, focus indicator) in Light and Dark, asserting AA ratios.
5. **Accessibility checklist** — a documented `@manual` checklist (screen-reader walkthrough,
   focus visibility, contrast) run as part of the DoD.

## Suggested design
- Expose contrast as a pure `@unit` check over `DesignTokens` (reuse the accessor from
  design-system-theming) so AA ratios are asserted without the UI; add button-fill/text and
  focus-indicator pairs to the ones already covered there.
- Accessible names are `@unit`-assertable against view-models/attached properties or via FlaUI's
  UIA name in `@ui`; prefer `@unit` where the name is a bound/attached value.
- Keep focus-trap logic in a reusable dialog base so every modal inherits it (proven once,
  reused everywhere).
- Treat "no control announced as unlabelled/pane" as the acceptance signal for the screen-reader
  pass (`@manual`).

## Test-first order
1. `@unit` accessible-name tests (primary controls, icon-only buttons, label association) → add
   `AutomationProperties` across views.
2. `@unit` contrast tests (body + button + focus indicator, both themes) → adjust tokens if any
   pair fails AA.
3. `@ui` keyboard-reachability, tab-order, and dialog focus-trap/restore tests → fix `TabIndex`,
   tab stops, and dialog base.
4. `@ui` focus-visible test → wire the shared focus visual.
5. `@manual` screen-reader + focus-in-both-themes checklist → PR walkthrough.

## Definition of done
- Every primary control exposes a non-empty accessible name; inputs are label-associated.
- Each main screen is fully operable by keyboard with logical tab order; dialogs trap and
  restore focus; focus is visibly indicated in both themes.
- Body text, primary button text, and the focus indicator meet WCAG-AA in Light and Dark.
- The `@manual` accessibility checklist is completed in the PR.

## Notes for later features
- Any new view MUST add automation names, correct tab order, and use the shared focus visual —
  add its controls to the label/contrast checks.
- `command-palette-shortcuts` supplies the keyboard entry points; this feature verifies they are
  discoverable and operable with visible focus.
- `v1-acceptance` (§9.1) relies on this pass for the "no unstyled/placeholder screens" bar.
