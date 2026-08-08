# Packet P3 — Conversations / M2 (biggest; contains a real app bug)

**Branch:** `feat/uigreen-p3`  •  **Read first:** `../PLAYBOOK.md`

## Owns (exclusive)
- `src/MeticulousResearch.App/Views/ConversationsView.xaml`
- `src/MeticulousResearch.App/ViewModels/Sections/ConversationsViewModel.cs`
- (and the turn/model/cost view-models it composes, if the fix lives there — coordinate via STATUS if
  a file is shared with another packet)

## Test files (your cluster)
- `Conversations`, `Streaming`, `TurnMetadataActions`, `CostTracking`, `ModelSelector`,
  `PromptCaching`, `ToolTransparency`, `ImageVision`, `ImageAttachments`, `RateLimitBackoff`,
  `ContextBudget` UiTests.

## Run
```powershell
dotnet build MeticulousResearch.sln -c Debug
dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "FullyQualifiedName~ConversationsUiTests|FullyQualifiedName~StreamingUiTests|FullyQualifiedName~TurnMetadataActionsUiTests|FullyQualifiedName~CostTrackingUiTests|FullyQualifiedName~ModelSelectorUiTests|FullyQualifiedName~PromptCachingUiTests|FullyQualifiedName~ToolTransparencyUiTests|FullyQualifiedName~ImageVisionUiTests|FullyQualifiedName~ImageAttachmentsUiTests|FullyQualifiedName~RateLimitBackoffUiTests|FullyQualifiedName~ContextBudgetUiTests"
```

## PRIMARY BLOCKER (a real app bug to fix)
With `METICULOUS_UI_FAKE_AI=1`, sending a message **already produces** user + assistant turns in
`ConversationThread` (verified). BUT `TurnActions` (visibility bound to `HasActions`) is **not
attached** after a streamed turn, so cost badge / metadata / action menu never appear. Almost every
M2 test depends on a completed turn exposing `TurnActions`.
- Investigate how `ConversationsViewModel` attaches the per-turn `Actions` VM after a turn completes
  (streaming path vs conversation path). The fake completes with usage `ChatCompleted(text, Usage(1200,300))`.
- Fix the owning side so a completed turn attaches its Actions/metadata (this is likely broken for real
  users too, like the workspace-section bug). Then `TurnActions`, `CostBadge`, `TurnMetadata`,
  `TurnActionMenu` (Copy/Retry/Edit&resend/Promote/Delete) surface.
- Ids present in `ConversationsView.xaml`: `MessageInput`, `SendButton`, `ConversationThread`,
  `TurnActions`, `CostBadge`, `CostBreakdown`, `TurnMetadata`, `Metadata*`, `TurnActionMenu`,
  `Copy/Retry/... Action`, `StreamingIndicator`, `InterruptedNotice`, `ResumeButton`, `TurnModelLabel`,
  `ModelPicker`, `CurrentModelLabel`, `ModelTiers`, `AllModelsExpander`, `VisionWarning`,
  `SwitchModelButton`, `ConversationRunningCost`, `RunningCost...`.

## Patterns
- Use `ShellUiFlow.OpenSection(window, "Conversations")`; type into `MessageInput`; click `SendButton`;
  **retry-wait** for the turn/`TurnActions` (PLAYBOOK async waits). The send-and-wait helper in each
  test file may need the retry-wait added.
- Empty-conversation empty-state test → use `OpenEmptyProject`.
- `ContextBudget` composer / `ImageAttachments` thumbnails / `ToolTransparency` tool activity all hang
  off the composer+thread — once `TurnActions`/turn rendering works, wire the specific affordance ids.
- Streaming interrupt/resume: the fake yields `ChatCancelled` on cancellation; assert `InterruptedNotice`
  / `ResumeButton` accordingly (may need a fake tweak — file a MASTER request if `FakeChatService`
  needs to support a cancel path differently).

## Definition of done
All listed clusters green; no regression; headless gate green; return the worker summary (list any
MASTER requests, e.g. `FakeChatService` behavior tweaks).
