# Phase — About Screen

**SPEC:** §3.7. **Milestone:** M5. **Depends on:** app-shell-navigation

## Goal
Provide a styled **About** screen showing app identity — product name, application icon, and
version — reachable from Settings (SPEC §3.7, §4(7)). Small but part of the "finished commercial
software" bar (§9.1(10)).

## Deliverables
1. **`IAppInfo`** — exposes product name and version read from the assembly's informational
   version (not a hard-coded literal), plus the app icon resource reference.
2. **About view + view-model** — displays icon, product name, version; styled with design-system
   tokens; opened from a Settings "About" entry and closable back to the prior screen.
3. **Settings entry point** — an "About" action wired into the Settings screen.

## Suggested design
- Read the version via reflection on the entry/executing assembly's
  `AssemblyInformationalVersion` so it tracks the build; expose it through `IAppInfo` so the
  value is `@unit`-assertable without the window.
- The app icon should reference the shared brand icon resource (owned later by
  `app-branding-icon` in M6); until that lands, reference the placeholder brand asset from the
  design system so the screen is never blank.
- Keep the view trivial — all testable state (name, version) lives on the VM via `IAppInfo`.

## Test-first order
1. `@unit` identity tests (product name, icon present) → `IAppInfo` + VM.
2. `@unit` version tests (shows version; equals assembly informational version) → version accessor.
3. `@ui` navigation tests (reachable from Settings; closes back) → About view + Settings entry.
4. `@manual` branded-presentation checklist → visual pass in the PR.

## Definition of done
- About shows product name "MeticulousResearch Desktop", the app icon, and the assembly version.
- Version is sourced from the assembly, not hard-coded.
- Reachable from Settings and closable; styled with no default WPF chrome.

## Notes for later features
- `app-branding-icon` (M6) supplies the final application icon; About should pick it up via the
  shared icon resource without further change.
- `update-notice` (M6) may surface "update available" near version info; leave room for it on the
  About screen.
