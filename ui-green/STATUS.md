# UI-Green STATUS board (master maintains this)

Baseline: **33/108 `@ui` passing**. Headless gate: **GREEN (646)**. Branch: `test/e2e-suite`.

## Packet status
| Packet | Owner branch | State | @ui pass in cluster | Notes |
|--------|--------------|-------|---------------------|-------|
| P1-resources     | `feat/uigreen-p1` | DONE (merged d146de0) | 10/10 | surfaced Extraction/Fetch/RemoveConfirm/SourceUri ids |
| P2-artifacts     | `feat/uigreen-p2` | DONE (merged 0d05d8c) | 11/11 | Promote now green (P3 wired TurnActions + test fixed) |
| P3-conversations | `feat/uigreen-p3` | WIP (13/21) | 13/21 | turn-actions/streaming/model/cost/prompt-caching/empty-state green; 8 remain |
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
- FIXED (P3): `ProjectWorkspaceViewModel` built `ConversationsViewModel` with only 5 args, omitting
  `turnActions`/`costCalculator`/`clipboard`/`retryStatus`/`cost`, so `AttachActions` always
  early-returned and no completed turn ever showed its cost badge / metadata / action menu. Now
  wired. Also surfaced `TurnActions`/`ModelPicker`/`ConversationEmptyState` (StackPanels don't
  surface to UIA) and added a per-token delay to `FakeChatService` so streaming/stop/interrupt are
  observable; CostTracking/PromptCaching send-helpers gained retry-waits.

### P3 remaining (8 tests — each needs real feature wiring / cross-packet)
- ContextBudget (2): compose the existing `ContextBudgetViewModel` into the composer; add
  `ConversationComposer`/`ContextBudgetMeter`/`ContextBudgetWarning`/`BudgetDeselectButton`/
  `BudgetSwitchModelButton` + an "estimated" label to `ConversationsView`.
- ImageAttachments (2): compose the existing `ConversationComposerViewModel` (attachments) into the
  composer/thread; add `ComposerAttachmentThumbnail`/`TurnAttachmentThumbnail`/`ImageAttachmentPreview`
  (needs a seeded/pasted attachment, no OS file dialog).
- ToolTransparency (1): render `TurnToolActivity` with per-tool names; needs the fake to emit tool
  events (Read/Write) and the turn VM/view to show them.
- RateLimitBackoff (1): register the @ui fake wrapped in `RetryingChatService` with a scripted 429
  so `RetryStatus`/`RetryingIndicator` fire (currently the fake replaces IChatService raw).
- ImageVision (1, CROSS-PACKET P1): seed an image resource (+cached caption) in `SampleProjectFactory`
  so the P1 Resources preview (`ResourceThumbnail`/`ResourcePreview`) has an image row to select.
- CostTracking dashboard (1, CROSS-PACKET P4): `ConsolidatedCostPanel`/`CostTotal`/`CostBySource`/
  `CostByModel`/`CostByWindow` live in `DashboardView` (P4-owned).

