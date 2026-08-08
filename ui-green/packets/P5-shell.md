# Packet P5 — Shell / Palette / Theme / Accessibility / States

**Branch:** `feat/uigreen-p5`  •  **Read first:** `../PLAYBOOK.md`

## Owns (exclusive)
- `src/MeticulousResearch.App/MainWindow.xaml` (+ `.xaml.cs` for palette/shortcut/focus code)
- `src/MeticulousResearch.App/Views/SectionView.xaml`
- `src/MeticulousResearch.App/Views/CommandPaletteView.xaml`
- `src/MeticulousResearch.App/Theme/*`
- Any **shared empty/loading/error controls** the states tests need (create them here if missing).

> Coordinate: `MainWindow.xaml` is shell-central. Only P5 edits it. If another packet needs a shell
> change, it files a MASTER request.

## Test files (your cluster)
- `CommandPalette`, `Theme`, `Accessibility`, `EmptyLoadingErrorStates`, `AppBranding`, `Settings`,
  `UpdateNotice`, `Onboarding` UiTests.

## Run
```powershell
dotnet build MeticulousResearch.sln -c Debug
dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "FullyQualifiedName~CommandPaletteUiTests|FullyQualifiedName~ThemeUiTests|FullyQualifiedName~AccessibilityUiTests|FullyQualifiedName~EmptyLoadingErrorStatesUiTests|FullyQualifiedName~AppBrandingUiTests|FullyQualifiedName~SettingsUiTests|FullyQualifiedName~UpdateNoticeUiTests|FullyQualifiedName~OnboardingUiTests"
```

## Sub-areas & what they need
- **CommandPalette:** Ctrl+K opens `CommandPaletteRoot`/`CommandPaletteSearchBox`; arrow/Enter drive
  `CommandPaletteResults`; Esc closes `CommandPaletteOverlay` and restores focus. Ids exist in
  `MainWindow.xaml`/`CommandPaletteView.xaml`; verify focus + keyboard patterns.
- **Theme:** toggling theme updates live; the styled-kit gallery controls (`GalleryButton`, `GalleryTextBox`,
  `GalleryComboBox`, `GalleryDataGrid`, `GalleryDialog`) must be reachable and styled (not default chrome).
  May need the `ThemeGallery`/`Design System` view reachable via a nav/command.
- **Accessibility:** keyboard-only reachability, logical tab order, visible focus indicator, dialog focus
  trap/restore. These assert on the shell + a screen; ensure focusable order + `AutomationProperties`.
- **EmptyLoadingErrorStates:** expects **generic** ids `EmptyState`, `EmptyStateMessage`,
  `EmptyStateCallToAction`, `SkeletonLoader`, `ErrorState`, `ErrorStateMessage`,
  `ErrorStateRecoveryButton`. The app currently uses per-section ids (`ArtifactsEmptyState`, …). Add a
  **shared designed empty/loading/error control** (or generic ids) so these scenarios pass honestly.
  Empty-list scenarios open a **fresh empty** project (`OpenEmptyProject`).
- **AppBranding / Settings / UpdateNotice / Onboarding:** window title = product name (gate-enforced),
  About/version, settings screen reachable via `OpenSettingsButton`, update-notice + onboarding wizard
  entry. Navigate to the app Settings screen before asserting `AppSettingsRoot` (click `OpenSettingsButton`).

## Definition of done
All listed clusters green; no regression; headless gate green; return the worker summary.
