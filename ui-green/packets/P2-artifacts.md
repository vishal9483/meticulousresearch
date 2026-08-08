# Packet P2 — Artifacts

**Branch:** `feat/uigreen-p2`  •  **Read first:** `../PLAYBOOK.md`

## Owns (exclusive)
- `src/MeticulousResearch.App/Views/ArtifactsView.xaml`
- `src/MeticulousResearch.App/ViewModels/Sections/ArtifactsViewModel.cs`

## Test files (your cluster)
- `tests/MeticulousResearch.UiTests/ArtifactCreationUiTests.cs`
- `tests/MeticulousResearch.UiTests/ArtifactDiffUiTests.cs`
- `tests/MeticulousResearch.UiTests/ArtifactVersioningUiTests.cs`
- `tests/MeticulousResearch.UiTests/EditWithClaudeUiTests.cs`
- `tests/MeticulousResearch.UiTests/BrandedExportUiTests.cs`

## Run
```powershell
dotnet build MeticulousResearch.sln -c Debug
dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "FullyQualifiedName~ArtifactCreationUiTests|FullyQualifiedName~ArtifactDiffUiTests|FullyQualifiedName~ArtifactVersioningUiTests|FullyQualifiedName~EditWithClaudeUiTests|FullyQualifiedName~BrandedExportUiTests"
```

## What these tests need
- The seeded sample has **1 artifact** ("Market Research Report"). `ArtifactsList` (ListBox) lists it;
  selecting it opens the editor/version rail.
- Ids: `ArtifactsRoot`, `ArtifactsList`, `ArtifactsEmptyState`, `NewArtifactButton`,
  `VersionHistoryRail`, `VersionHistoryItem`, `CurrentVersionMarker`, `DeleteArtifactButton`,
  `DeleteArtifactConfirmDialog`, diff ids (`ArtifactDiffPanel`, `DiffBasePicker`, `DiffComparePicker`,
  `DiffSideBySideButton`, `DiffInlineButton`, `DiffSideBySideView`, `DiffLeftPane`, `DiffRightPane`,
  `DiffInlineView`, `DiffDisabledHint`), edit-with-claude ids (`EditWithClaudeBar`, `...Instruction`,
  `...Button`, `...Preview`), branded-export ids (`BrandedExportBar`, `...Menu`, `...FormatPicker`,
  `...PreviewButton`, `...Preview`, `...Confirm`).

## KNOWN DEPENDENCY — ArtifactDiff needs ≥2 versions
The diff scenarios need an artifact with **two versions** (side-by-side / inline / "unavailable with a
single version"). The seed makes **one** version. Options (pick the honest one):
1. Preferred: **propose a MASTER change** to `SampleProjectFactory` (Core, shared-infra) to add a
   second version to the sample artifact (e.g. an edit-with-claude/CreateFromContent revision). Put the
   exact request in `../STATUS.md` "Requests to MASTER"; do NOT edit Core yourself.
2. Or drive a second version **through the app UI** inside the test (edit-with-claude with the fake AI
   produces a new version) before asserting the diff.
- The "Diff mode unavailable with a single version" test is satisfied by a **fresh** artifact with one
  version — consider `OpenEmptyProject` + create one artifact for that specific scenario.

## Likely fixes
- Surface `ArtifactsEmptyState` and any diff/edit/export container ids per the surfacing rule.
- `EditWithClaudeButton`/generation uses the fake AI (already registered) → returns deterministic text;
  add retry-waits for the produced preview/new version.
- Branded export: assert the preview/confirm affordances; if the export action isn't wired to a
  surfaced control, wire it.

## Definition of done
All five clusters green; no regression; headless gate green; return the worker summary (include any
MASTER request you filed).
