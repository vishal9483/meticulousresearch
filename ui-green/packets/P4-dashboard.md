# Packet P4 — Dashboard / Usage CSV

**Branch:** `feat/uigreen-p4`  •  **Read first:** `../PLAYBOOK.md`

## Owns (exclusive)
- `src/MeticulousResearch.App/Views/DashboardView.xaml`
- `src/MeticulousResearch.App/ViewModels/Sections/DashboardViewModel.cs`

## Test files (your cluster)
- `tests/MeticulousResearch.UiTests/UsageCsvExportUiTests.cs`

## Run
```powershell
dotnet build MeticulousResearch.sln -c Debug
dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "FullyQualifiedName~UsageCsvExportUiTests"
```

## What these tests need
- Open the sample project's **Dashboard** (default section) via `ShellUiFlow.OpenSampleProject` (already
  called by the helper). The dashboard already surfaces `ResourceCount`, `ArtifactCount`,
  `ConversationCount`, `CostTotal`, `CostByModel`, `ExportUsageCsvButton`, `ExportUsageCsvConfirmation`.
- The CSV-export scenario clicks `ExportUsageCsvButton` and asserts a confirmation
  (`ExportUsageCsvConfirmation`) — the export writes to disk; make sure the click path is wired and the
  confirmation text surfaces (retry-wait).

## Notes / coordination
- `CostTrackingUiTests` (dashboard cost panel + conversation cost badge) is owned by **P3** (it sends
  messages). Do not edit `CostTrackingUiTests` here. If a dashboard-only id it needs is missing, add it
  to `DashboardView.xaml` (yours) and note it in STATUS so P3 knows.
- Any queried container id on a `Grid`/`StackPanel`/`Border` → surfacing rule.

## Definition of done
UsageCsvExport green; no regression; headless gate green; return the worker summary.
