# UI-Green PLAYBOOK — shared technical knowledge

Read this once. It encodes everything hard-won so far so you don't rediscover it. Then work only
inside your packet.

## How the @ui harness works
- `tests/MeticulousResearch.UiTests` drives the **built WPF app** via FlaUI (UIA3). One app process
  is **shared across the whole `shell-ui` collection** and tests run **sequentially** → tests must be
  **order-independent** (always navigate to the state you need; never assume the prior test's state).
- `ShellUiFixture` launches the app against a **clean temp data dir** and sets two flags:
  - `METICULOUS_UI_SEED=1` → on startup the app seeds one **offline sample project**
    (`SampleContent.ProjectName = "Sample: EV Battery Market 2026"`) with **2 resources + 1 artifact**
    (no AI, no key). Dashboard shows ResourceCount=2, ArtifactCount=1.
  - `METICULOUS_UI_FAKE_AI=1` → the app registers a deterministic `FakeChatService` so **sending a
    message produces user+assistant turns** with fixed usage (1200 in / 300 out), no network/key.
- Build then run:
  ```powershell
  dotnet build MeticulousResearch.sln -c Debug
  dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "FullyQualifiedName~<YourCluster>UiTests"
  ```
  The app exe is NOT rebuilt by building the test project alone — **always build the solution** (or
  the App project) after changing app XAML/VM, or FlaUI launches a stale exe.

## THE SURFACING RULE (root cause of most `Assert.NotNull(...Root/...)` failures)
FlaUI's control-view UIA tree only contains certain WPF element types. An `AutomationId` set on a
**non-surfacing** element is invisible to `FindFirstDescendant(cf => cf.ByAutomationId(...))`.

- **Surfaces (id is findable):** `UserControl` (=Custom), `ItemsControl`/`ListBox`/`ListView`/`DataGrid`
  (=List/DataGrid), `ScrollViewer` (=Pane), `Button`, `TextBox` (=Edit), `TextBlock` (=Text),
  `RadioButton`, `CheckBox`, `Expander`, `Menu`.
- **Does NOT surface:** `Grid`, `StackPanel`, `Border`, `ContentControl`.

**Fixes:**
- A **screen-root** id (e.g. `XxxRoot`) → move it onto the `UserControl` root:
  `<UserControl ... AutomationProperties.AutomationId="XxxRoot" ...>` and drop it from the inner Grid.
- A **pane/empty-state/dialog** container id that must stay a panel → wrap its content in a
  `<ScrollViewer AutomationProperties.AutomationId="Xxx" VerticalScrollBarVisibility="Disabled"
  HorizontalScrollBarVisibility="Disabled"> ... </ScrollViewer>` (keeps layout, surfaces the id).
- A **DataGrid** whose rows must be findable by cell text → set
  `EnableRowVirtualization="False" VirtualizingPanel.IsVirtualizing="False"` (virtualized rows are
  absent from the UIA tree). Cell text surfaces as a `Text` element `Name`.

Verify what's actually in the tree with a throwaway probe (see "Probing" below) — never guess.

## The shared flow helper — `ShellUiFlow` (MASTER-OWNED; do not edit, just call)
```csharp
ShellUiFlow.EnsureAtHome(window)        // -> ProjectsHomeRoot (clears the search filter)
ShellUiFlow.OpenSampleProject(window)   // -> WorkspaceRoot of the SEEDED populated sample
ShellUiFlow.OpenEmptyProject(window)    // -> WorkspaceRoot of a FRESH empty project (for empty-state tests)
ShellUiFlow.OpenSection(window, "Resources") // opens sample + selects the left-nav section -> CenterPane
```
- **Content tests** (list has data, versions, cost) → open the **populated sample** via
  `OpenSampleProject` / `OpenSection`.
- **Empty-state tests** ("No X yet") → open a **fresh empty** project via `OpenEmptyProject`, then
  navigate to the section.

## Async waits (turns, content swaps)
After an action that streams or swaps a view, **do not** look immediately — wrap in a retry:
```csharp
var el = FlaUI.Core.Tools.Retry.WhileNull(
    () => parent.FindFirstDescendant(cf => cf.ByAutomationId("X")),
    System.TimeSpan.FromSeconds(10)).Result;
Assert.NotNull(el);
```

## TextBox binding commit
Setting `textBox.Text = "..."` via UIA updates the control; a `LostFocus`-triggered binding commits
when focus leaves (e.g. when you click the Send/Add button). If a command sees an empty value, the
app TextBox likely needs `UpdateSourceTrigger=PropertyChanged` — that's an **app-view fix owned by
the packet**.

## Faithfulness (do NOT cheat a test green)
- Never soften/skip/tautologize an assertion. If a test needs data that doesn't exist, make it exist
  the honest way (seed opens the sample; empty tests open a fresh project).
- If a required control/affordance genuinely isn't wired in the app, **wire it** (that's the fix) —
  don't delete the assertion. Several failures are **real app bugs** (e.g. the workspace was building
  `ResourcesViewModel`/`ArtifactsViewModel` with the serviceless ctor so sections never loaded — now
  fixed in `ProjectWorkspaceViewModel`). Expect to find more; fix the owning side.
- A cross-feature/contract conflict (e.g. window title) is reconciled to the value the **headless
  gate enforces**, with a comment — never by weakening the gate.

## Probing (throwaway diagnostics)
To see the real tree, drop a temporary probe test in the UiTests project and dump
`window.FindAllDescendants()` as `ControlType|AutomationId|Name` to a temp file, run it with a
`FullyQualifiedName~` filter, read the file, then **delete the probe**. Guard property reads in
try/catch (stale elements throw).

## Gotchas (verified)
- **`create_file` sometimes writes an EMPTY/missing file.** After creating any file, verify with
  `(Get-Item <path>).Length`; if 0/missing, rewrite (create_file again, or terminal `Set-Content`).
- **Editor-buffer staleness:** an edit tool may report success without touching disk. After editing
  app/test files, verify on disk with `Select-String` before trusting it.
- **Never `Start-Sleep`-poll** the terminal; sync commands return when done.
- Only .NET SDK 7 & 9 are installed; `global.json` roll-forward builds `net8.0` fine.

## Merge gate (must stay green after every change)
```powershell
dotnet test MeticulousResearch.sln -c Debug --filter "Category!=ui&Category!=manual"
```
`@ui` and `@manual` are excluded from the gate by design; your job is to make `@ui` pass when run
explicitly, without breaking the gate.
