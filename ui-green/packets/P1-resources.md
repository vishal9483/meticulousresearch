# Packet P1 — Resources

**Branch:** `feat/uigreen-p1`  •  **Read first:** `../PLAYBOOK.md`

## Owns (exclusive — no other packet edits these)
- `src/MeticulousResearch.App/Views/ResourcesView.xaml`
- `src/MeticulousResearch.App/ViewModels/Sections/ResourcesViewModel.cs`

## Test files (your cluster)
- `tests/MeticulousResearch.UiTests/ResourcesUiTests.cs`
- `tests/MeticulousResearch.UiTests/ResourceManagementUiTests.cs`
- `tests/MeticulousResearch.UiTests/SearchUiTests.cs`
- `tests/MeticulousResearch.UiTests/TokenEstimationUiTests.cs`
- `tests/MeticulousResearch.UiTests/UrlResourceUiTests.cs`
- `tests/MeticulousResearch.UiTests/FileUploadUiTests.cs`

## Run
```powershell
dotnet build MeticulousResearch.sln -c Debug
dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "FullyQualifiedName~ResourcesUiTests|FullyQualifiedName~ResourceManagementUiTests|FullyQualifiedName~SearchUiTests|FullyQualifiedName~TokenEstimationUiTests|FullyQualifiedName~UrlResourceUiTests|FullyQualifiedName~FileUploadUiTests"
```

## What these tests need (asserted ids / behaviors)
- The Resources section opens on the **seeded sample** (2 resources) via `OpenSampleProject` — the
  cluster helpers already call it. `ResourcesTable` (DataGrid) must list rows **by cell text**
  (row virtualization is already disabled).
- `ResourcesTable`, `ResourceTitleInput`, `ResourceTextInput`, `AddPastedTextButton`, `ResourcePreview`,
  `ResourceSearchBox`, `ResourcesNoSearchMatches`, `ResourcesEmptyState`, `ResourcePreviewMetadata`,
  `ResourceUrlInput`, `AddUrlButton`, `UploadFileButton`, `ResourcesEnabledTotal`, token-estimate column.
- After **adding** a pasted resource, the new row must appear in the table (retry-wait; check the
  add TextBoxes commit — see PLAYBOOK "TextBox binding commit").
- URL / file-upload add flows must surface their result rows/errors.

## Likely fixes
- Surface any queried container id sitting on a `Grid`/`StackPanel`/`Border` (PLAYBOOK surfacing rule).
- Ensure add-resource TextBoxes use `UpdateSourceTrigger=PropertyChanged` if the add command sees empty.
- Add retry-waits in the **test** helpers for post-add / post-select lookups.
- Empty-state tests (if any assert "No resources yet") → switch that test to `OpenEmptyProject`.

## Definition of done
All six clusters green; previously-green subset not regressed; headless gate green; return the
worker summary.
