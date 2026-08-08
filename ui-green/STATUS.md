# UI-Green STATUS board (master maintains this)

Baseline: **33/108 `@ui` passing**. Now: **79/108 `@ui` passing**. Headless gate: **GREEN (646)**.
Branch: `test/e2e-suite`. Integration tip: **`1a3b37a`** (P1–P6 merged).

## Packet status
| Packet | Owner branch | State | @ui pass in cluster | Notes |
|--------|--------------|-------|---------------------|-------|
| P1-resources     | `feat/uigreen-p1` | DONE (merged d146de0) | 10/10 | surfaced Extraction/Fetch/RemoveConfirm/SourceUri ids |
| P2-artifacts     | `feat/uigreen-p2` | DONE (merged 0d05d8c) | 11/11 | Promote now green (P3 wired TurnActions + test fixed) |
| P3-conversations | `feat/uigreen-p3` | MERGED (f9e6e73), 18/21 | 18/21 | ImageAttachments (2) + ToolTransparency (1) remain (B1/B2) |
| P4-dashboard     | `feat/uigreen-p4` | DONE (merged bcae184) | 1/1 | wired ExportUsageCsv click path (save dialog / @ui temp path) |
| P5-shell         | `feat/uigreen-p5` | DONE (merged 7b89d71) | 18/31 | Theme(7)/CommandPalette(5)/AppBranding(3)/Onboarding(2)/Settings(1) green; Accessibility(4)/Esc(1)/EmptyStates(6)/UpdateNotice(2) → blockers |
| P6-projects      | `feat/uigreen-p6` | DONE (merged 1a3b37a) | 2/4 | BackupRestore(2) green; DeliverableTemplates New-artifact (screen-presence) + New-project (ordering) remain |
| P7-v1            | `feat/uigreen-p7` | TODO | – | V1Acceptance(9) — integration journeys; several may be ENV/live-API |

## Regression note (important)
An always-visible full-surface **command-palette overlay** (attempted for the Esc test) regressed
~40 shared-session content tests via hit-test interference. Reverted. Lesson: **no persistent
full-surface overlay**. The @ui-only behind-content mounts (ThemeGallery/Onboarding/Settings) are
safe because they don't intercept and are covered by the content region.

## Autonomous run
Master flows P1→P7 unattended; logs anything it can't green honestly to `BLOCKERS.md` and continues.
No human sign-off between milestones. Merging to `main`/pushing still needs an explicit human ask.

## Next session — start here
Branch off the integration tip `f9e6e73` (`test/e2e-suite`). Two P3 tests remain (see the P3
"remaining" section below and `BLOCKERS.md` B1/B2), then P4–P6. To finish P3: `git checkout -b feat/uigreen-p3b test/e2e-suite`,
implement ImageAttachments + ToolTransparency, verify the P3 cluster + headless gate, merge back.
Cluster re-run filter:
```powershell
dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "FullyQualifiedName~ImageAttachmentsUiTests|FullyQualifiedName~ToolTransparencyUiTests"
```

## Already green (do not regress)
ShellNavigation (7), NoPlaceholder (6), About (2), ProjectsCrud (4), and much of Resources/Artifacts.

## Requests to MASTER (shared-infra changes proposed by workers)
Workers append here instead of editing shared infra; master applies then rebroadcasts.
- APPLIED (P2, by master): registered `IEditWithClaudeService` + `IExportService` in
  `ServiceConfiguration`; `SampleProjectFactory` now adds 2 more versions to the sample artifact
  (3 total) via `AddVersion`; added `MarketResearchReportV2/V3` to `SampleContent`.
- APPLIED (P3, by master): `ProjectWorkspaceViewModel` now passes `turnActions`/`costCalculator`/
  `clipboard`/`retryStatus`/`cost` + `resources`/`budgetService` into `ConversationsViewModel`;
  `FakeChatService` has a per-token delay + faults-once on a rate-limit prompt; the @ui `IChatService`
  is wrapped in `RetryingChatService`; the @ui `IResourceService` uses `SampleImageCaptioner` +
  caption-on-add; `App.xaml.cs` (@ui-only) seeds a captioned image resource and sets a low
  `ContextBudget`. **These are the master-owned shared-infra seams — a future worker must not edit
  them directly; file a request here.**

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

