# UI-Green — Parallel Work Kit

Goal: drive the FlaUI `@ui` suite (`tests/MeticulousResearch.UiTests`, `Category=ui`, 108 tests) to
green **without regressing the headless merge gate**, by splitting the remaining work into
**conflict-free packets** that a master agent can hand to worker subagents in parallel.

> Current baseline: **33/108 `@ui` passing**, headless gate GREEN (646). See `STATUS.md`.

## Read order (for any agent)
1. This file (how the work is divided + the rules).
2. `PLAYBOOK.md` — the hard-won technical knowledge (surfacing rule, `ShellUiFlow` API, the two
   launch flags, gotchas, verification commands). **Every worker reads this.**
3. Your assigned `packets/<id>.md` — the self-contained job for one packet.
4. Only the files your packet names. Do **not** browse the whole repo (context-window discipline).

## The packets (each owns disjoint app files → safe to run in parallel)

| Packet | Owns (app files — exclusive) | Test files (cluster) |
|--------|------------------------------|----------------------|
| `P1-resources`   | `Views/ResourcesView.xaml`, `ViewModels/Sections/ResourcesViewModel.cs` | Resources, ResourceManagement, Search, TokenEstimation, UrlResource, FileUpload |
| `P2-artifacts`   | `Views/ArtifactsView.xaml`, `ViewModels/Sections/ArtifactsViewModel.cs` | ArtifactCreation, ArtifactDiff, ArtifactVersioning, EditWithClaude, BrandedExport |
| `P3-conversations` | `Views/ConversationsView.xaml`, `ViewModels/Sections/ConversationsViewModel.cs` | Conversations, Streaming, TurnMetadataActions, CostTracking, ModelSelector, PromptCaching, ToolTransparency, ImageVision, ImageAttachments, RateLimitBackoff, ContextBudget |
| `P4-dashboard`   | `Views/DashboardView.xaml`, `ViewModels/Sections/DashboardViewModel.cs` | UsageCsvExport |
| `P5-shell`       | `MainWindow.xaml`, `Views/SectionView.xaml`, `Views/CommandPaletteView.xaml`, `Theme/*`, shared empty/loading/error controls | CommandPalette, Theme, Accessibility, EmptyLoadingErrorStates, AppBranding, Settings, UpdateNotice, Onboarding |
| `P6-projects`    | `Views/ProjectsHomeView.xaml`, `Views/TemplateGalleryView.xaml`, backup/template entry points | BackupRestore, DeliverableTemplates |
| `P7-v1`          | none (integration only) | V1Acceptance — **do LAST**, after P1–P6 are green |

## Conflict rules (critical for parallelism)
- A packet **exclusively owns** its app files. No two packets edit the same `.xaml`/VM.
- **Shared infra is MASTER-ONLY**: `tests/.../ShellUiFlow.cs`, `tests/.../ShellUiFixture.cs`,
  `src/.../ServiceConfiguration.cs`, `src/.../App.xaml.cs`, `src/.../Services/FakeChatService.cs`,
  `ViewModels/ProjectWorkspaceViewModel.cs`. A worker that needs a change here **must not edit it** —
  it reports the exact change in its summary and the master applies it once, then rebroadcasts.
- **Never** touch `docs/`, `global.json`, `Directory.Build.props`, or the CI filter.
- Each worker works on its own branch `feat/uigreen-<packet>` off the moving integration tip; the
  master merges sequentially and re-runs the gate after each merge.

## Definition of done (per packet)
- Every listed test passes: `dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "<cluster filter>"`.
- No regression: the previously-green `@ui` subset still passes.
- Headless gate green: `dotnet test MeticulousResearch.sln -c Debug --filter "Category!=ui&Category!=manual"`.
- A compact structured summary is returned (see the worker agent file).

## Files in this kit
- `PLAYBOOK.md` — shared technical knowledge (read once).
- `STATUS.md` — the live status board the master maintains.
- `packets/P1..P7.md` — self-contained job descriptions.
- `../.github/agents/ui-green-master.agent.md` and `ui-green-worker.agent.md` — agent definitions.
