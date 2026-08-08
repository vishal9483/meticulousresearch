# UI-Green STATUS board (master maintains this)

Baseline: **33/108 `@ui` passing**. Headless gate: **GREEN (646)**. Branch: `test/e2e-suite`.

## Packet status
| Packet | Owner branch | State | @ui pass in cluster | Notes |
|--------|--------------|-------|---------------------|-------|
| P1-resources     | `feat/uigreen-p1` | DONE (merged d146de0) | 10/10 | surfaced Extraction/Fetch/RemoveConfirm/SourceUri ids |
| P2-artifacts     | `feat/uigreen-p2` | DONE (merged 0d05d8c) | 11/11 | Promote now green (P3 wired TurnActions + test fixed) |
| P3-conversations | `feat/uigreen-p3` | WIP (18/21) | 18/21 | ContextBudget/RateLimit/ImageVision/dashboard done; 3 remain (ImageAttachments, ToolTransparency) |
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

### P3 remaining (3 tests — unimplemented features with seeding/driving challenges)
- ImageAttachments (2): needs per-turn image attachments rendered in the thread + a composer
  attachment thumbnail. Test 2 never sends, so it needs a **seeded conversation turn with an
  attachment** loaded on open (risks the 18 green conversation tests), and the composer needs a
  pending attachment without a file dialog (UIA can't drive one). Composer/turn thumbnail rendering
  + click-to-preview overlay must be built.
- ToolTransparency (1): needs per-turn tool activity (`TurnToolActivity` with Read/Write). There is
  **no tool-call `ChatEvent`** — the fake can't emit tool calls; requires wiring `ToolCallLog`/
  `ToolCallRecord` into turn rendering + seeding tool activity.

### P3 done this iteration
- ContextBudget (2): composer `ConversationComposer` + `ContextBudgetMeter`/`ContextBudgetWarning`
  (deselect/switch), low @ui budget so the sample is genuinely over budget.
- RateLimitBackoff (1): @ui fake wrapped in `RetryingChatService`, faults-then-succeeds on a
  rate-limit prompt; surfaced `RetryingIndicator`.
- ImageVision (1): @ui-only seeded image resource + deterministic offline captioner (caption-on-add);
  sample stays at 2 resources for the onboarding @unit tests.
- CostTracking dashboard (1): surfaced the existing `ConsolidatedCostPanel`/`CostBySource`/`CostByWindow`
  (were on non-surfacing Border/StackPanel).

