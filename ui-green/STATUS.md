# UI-Green STATUS board (master maintains this)

Baseline: **33/108 `@ui` passing**. Headless gate: **GREEN (646)**. Branch: `test/e2e-suite`.

## Packet status
| Packet | Owner branch | State | @ui pass in cluster | Notes |
|--------|--------------|-------|---------------------|-------|
| P1-resources     | `feat/uigreen-p1` | DONE (merged d146de0) | 10/10 | surfaced Extraction/Fetch/RemoveConfirm/SourceUri ids |
| P2-artifacts     | `feat/uigreen-p2` | DONE (merged 0d05d8c) | 10/11 | Promote test blocked on P3 TurnActions; rest green |
| P3-conversations | `feat/uigreen-p3` | WIP  | – | biggest; contains the TurnActions/HasActions app bug |
| P4-dashboard     | `feat/uigreen-p4` | TODO | – | UsageCsvExport |
| P5-shell         | `feat/uigreen-p5` | TODO | – | needs generic EmptyState/SkeletonLoader/ErrorState ids |
| P6-projects      | `feat/uigreen-p6` | TODO | – | Backup + Template gallery entry points |
| P7-v1            | `feat/uigreen-p7` | BLOCKED | – | start only after P1–P6 green |

## Already green (do not regress)
ShellNavigation (7), NoPlaceholder (6), About (2), ProjectsCrud (4), and much of Resources/Artifacts.

## Requests to MASTER (shared-infra changes proposed by workers)
Workers append here instead of editing shared infra; master applies then rebroadcasts.
- APPLIED (P2, by master): registered `IEditWithClaudeService` + `IExportService` in
  `ServiceConfiguration`; `SampleProjectFactory` now adds 2 more versions to the sample artifact
  (3 total) via `AddVersion`; added `MarketResearchReportV2/V3` to `SampleContent`.

## Real app bugs found (fix the owning side; track here)
- FIXED: `ProjectWorkspaceViewModel` built `ResourcesViewModel`/`ArtifactsViewModel` with the
  serviceless ctor → sections never loaded content. Now wired with the section services.
- OPEN (P3): after a streamed turn via the fake, `TurnActions`/`HasActions` is not attached →
  turn-metadata/cost/action affordances never appear. Investigate `ConversationsViewModel` post-turn
  Actions attachment.
