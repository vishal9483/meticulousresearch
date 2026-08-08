# Packet P6 — Projects / Backup / Templates

**Branch:** `feat/uigreen-p6`  •  **Read first:** `../PLAYBOOK.md`

## Owns (exclusive)
- `src/MeticulousResearch.App/Views/ProjectsHomeView.xaml`
- `src/MeticulousResearch.App/Views/TemplateGalleryView.xaml`
- The **backup** and **template-gallery** entry-point controls/wiring (add where missing).

> `ProjectsHomeView.xaml` is also where ProjectsCrud (already green) lives — don't regress it.

## Test files (your cluster)
- `tests/MeticulousResearch.UiTests/BackupRestoreUiTests.cs`
- `tests/MeticulousResearch.UiTests/DeliverableTemplatesUiTests.cs`

## Run
```powershell
dotnet build MeticulousResearch.sln -c Debug
dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "FullyQualifiedName~BackupRestoreUiTests|FullyQualifiedName~DeliverableTemplatesUiTests"
```

## What these tests need
- **BackupRestore:** in an open project workspace, a `BackupProjectButton` → writes a zip →
  `BackupProjectConfirmation`. On the Projects home, a `RestoreProjectButton` → picks a zip → restored
  project appears in `ProjectsList`. These entry points may not be wired yet — **wire them** to the
  existing `IProjectBackupService` (already in DI). File-dialog interactions should be driven headlessly
  (e.g. a pre-set path / injected chooser) — if the app hard-codes a picker, add a testable seam and
  note it in STATUS.
- **DeliverableTemplates:** "New artifact" flow opens `TemplateGallery` with `TemplateGalleryItem`/
  `TemplateName`/`TemplateDescription`/`TemplatePreview`; "New project" flow (from home) also opens the
  template gallery. Wire the `NewArtifactButton` / "New project" entry points to the
  `TemplateGalleryViewModel` (registered in DI) so the gallery is reachable.

## Notes
- Prefer honest wiring over test-only shortcuts. If backup needs a destination without a modal picker,
  add a small injectable path seam in the App layer (yours) rather than faking the assertion.
- Surface any queried container id per the surfacing rule.

## Definition of done
Both clusters green; ProjectsCrud not regressed; headless gate green; return the worker summary.
