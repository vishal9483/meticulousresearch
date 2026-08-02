# Phase — App Shell & Navigation

**SPEC:** §4, §7.1. **Milestone:** M0. **Depends on:** — (this is the first UI feature).

## Goal
Stand up the WPF application shell (window, MVVM host, DI, navigation) that every later feature
plugs into. This feature owns the **navigation contract** and the **three-pane workspace layout**.

## Deliverables
1. **Solution & projects** (if not already created by data-store-migrations):
   - `src/MeticulousResearch.App` (WPF, .NET 8, `net8.0-windows`).
   - `src/MeticulousResearch.Core` (class library) — referenced by App.
   - `tests/MeticulousResearch.App.Tests` (xUnit), `tests/MeticulousResearch.UiTests` (FlaUI).
2. **DI + MVVM host** using `Microsoft.Extensions.Hosting` + `CommunityToolkit.Mvvm`.
3. **Navigation service** — `INavigationService` with `NavigateTo<TViewModel>(params)`,
   `Back()`, `CurrentViewModel`, `ActiveProjectId`.
4. **Shell**: `MainWindow` + `ShellViewModel` with a `CurrentViewModel` content region and a
   left navigation region.
5. **Three-pane workspace**: `ProjectWorkspaceView` (left nav / center content / right contextual)
   + `ProjectWorkspaceViewModel`.
6. **Placeholder-free destinations**: each section resolves to a real (possibly minimal but
   designed) view registered in DI.

## Suggested design
- `ShellViewModel` holds `CurrentViewModel` (ObservableObject). Views map to view-models via a
  `DataTemplate` dictionary keyed by VM type (MVVM view-location).
- `INavigationService` keeps a back-stack (`Stack<NavEntry>`); `NavEntry` captures VM type + params.
- `ProjectWorkspaceViewModel` owns child VMs (Conversations/Resources/Artifacts/Dashboard/Settings)
  and an `ActiveSection` enum; switching section swaps the center `SectionViewModel`.
- Keep view-models constructor-injected so `App.Tests` can new them up with fakes.

## Test-first order
1. `@unit` navigation contract tests (ShellViewModel, NavigationService, ProjectWorkspaceViewModel)
   → implement service + VMs.
2. `@ui` shell/workspace tests via FlaUI → implement `MainWindow`, `ProjectWorkspaceView`, DataTemplates.
3. `@ui` "no placeholder" + resize tests → ensure every section has a real registered view.

## Definition of done
- All `@unit` nav scenarios green; `@ui` shell scenarios green.
- App launches to Projects home; project opens to three-pane workspace; all left-nav sections
  render real views. No blank/"not implemented" screens.

## Notes for later features
- Later features add their real views by registering a VM + DataTemplate and a nav entry; they
  must not replace the shell or navigation contract.
- Theming (`design-system-theming`) layers styles on top of these views — keep control usage
  standard so styles apply globally.
