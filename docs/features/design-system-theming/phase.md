# Phase — Design System & Theming

**SPEC:** §3.7 (+ §8 contrast). **Milestone:** M0. **Depends on:** app-shell-navigation

## Goal
Establish the visual identity: a corporate navy palette, light/dark/system themes, and a styled
Fluent control kit so no screen shows unstyled default WPF chrome. Owns the **theme resources**
and **design tokens** every other view uses.

## Deliverables
1. **Control library** — adopt WPF-UI (or equivalent Fluent kit) and set up its resource
   dictionaries in `App.xaml`.
2. **Design-token dictionaries** — `Light.xaml` / `Dark.xaml` defining: primary navy, single
   accent, neutral surfaces, semantic success/warning/error, plus a type scale (UI sans + report
   serif/sans option) and 8px spacing units.
3. **`IThemeService`** — `SetTheme(Light|Dark|System)`, `CurrentTheme`, live switch by swapping
   merged dictionaries; resolves System from the OS setting and reacts to OS changes.
4. **Persistence** — theme stored via `ISettingsService` (from settings-secure-key) or, if that's
   not yet available in build order, a local setting; must survive restart.
5. **Styled defaults** — global styles so standard Button/TextBox/ComboBox/DataGrid/Dialog/Toast
   pick up the kit automatically (implicit styles keyed by type).
6. **Contrast tokens** chosen to meet WCAG-AA (≥4.5:1 body text) in both themes.

## Suggested design
- Keep tokens semantic (e.g. `SurfaceBrush`, `OnSurfaceBrush`, `AccentBrush`) so views reference
  roles, not raw colors — themes then swap cleanly.
- `IThemeService.SetTheme` swaps a single merged dictionary at the app level for live updates.
- Expose token color values to `@unit` tests (e.g. a `DesignTokens` accessor) so palette and
  contrast can be asserted without the UI.

## Test-first order
1. `@unit` token-presence + contrast tests → define token dictionaries + `DesignTokens` accessor.
2. `@unit` theme-resolve + persistence tests → implement `IThemeService`.
3. `@ui` live-switch + styled-control tests → wire dictionaries and implicit styles.
4. `@manual` coherence checklist → visual pass in the PR.

## Definition of done
- Light/Dark/System all resolve and persist; live switch works with no restart.
- Required tokens exist; body-on-surface contrast ≥4.5:1 in both themes.
- Standard controls render with the styled kit; no default WPF chrome.

## Notes for later features
- `empty-loading-error-states` and `accessibility` build on these tokens; keep them stable and
  semantic.
- `branded-export` has its own document theme (cover/TOC), but should reuse the accent/logo
  settings surfaced here and in Settings.
- All feature views MUST use semantic tokens rather than hard-coded colors.
