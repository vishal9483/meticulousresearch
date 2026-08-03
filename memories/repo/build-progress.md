# MeticulousResearch build progress

## Milestones
- M0: COMPLETE (merged into main)
- M1: COMPLETE (merged into main)
- M2: IN PROGRESS. base=main, integration tip=main

## M2 feature order (dependency-respecting)
1. ai-gateway (CONTRACT) - pending
2. builtin-file-tools-sandbox - pending
3. conversations - pending
4. model-selector (CONTRACT) - pending
5. streaming - pending
6. turn-metadata-actions - pending
7. image-attachments - pending
8. rate-limit-backoff - pending
9. prompt-caching - pending

## Baseline gate on main at M2 start
App.Tests: 45 passed; Core.Tests: 161 passed; total 206.

## Results log
(append one line per feature)
ai-gateway (CONTRACT): APPROVED after 1 attempt, merged into main, gate=App45/Core187+1skip. Contract=IChatService Ask->IAsyncEnumerable<ChatEvent>, ChatUsage, ChatErrorClassifier, IChatBackendFactory(ISettingsService.ChatBackend), FakeChatService, IArtifactService seam.
builtin-file-tools-sandbox: APPROVED after 1 attempt, merged into main, gate=App45/Core202+1skip. Contract: BuiltInToolSet(7-tool registry), ProjectSandbox, ProjectToolInvoker, ToolCallLog/ToolCallRecord (reused by turn-metadata-actions), FakeArtifactService in TestSupport. NOTE: one isolated flaky failure observed post-merge, green on 4 subsequent runs -> watch.
conversations: APPROVED after 1 attempt, merged into main, gate=App45/Core213+1skip. Contract: IConversationService, ConversationGroundingAssembler(->ChatAskContext), persists Message rows(model/tokens/latency/resource_scope). Downstream: model-selector supplies per-turn model, streaming replaces Ask loop, turn-metadata-actions consumes metadata, cost-tracking fills CostUsd.
model-selector (CONTRACT): APPROVED after 1 attempt, merged into main, gate=App49/Core226+1skip. Contract: IModelCatalog(Tiers/Resolve/TryGet/IsVisionCapable/GetPrice->ModelPrice), ModelInfo, ModelSelection(conv default + per-msg override, ids stored), ModelVisionAdvisor/VisionWarning, ModelPickerViewModel. Downstream: gateway/streaming send Resolve(...).Id; cost-tracking reads GetPrice; image-attachments uses IsVisionCapable.
streaming: APPROVED after 1 attempt, merged into main, gate=App49/Core233+1skip. Contract: IStreamingConversationService(StreamAsk/Resume), StreamingTurn(IsInterrupted/IsRetryable/State), StreamingState. Partial text persisted to Message table; interrupted marker in-memory on StreamingTurn. Downstream: rate-limit-backoff auto-retry before interrupted-persist, turn-metadata-actions retry=resume.
turn-metadata-actions: APPROVED after 1 attempt, merged into main, gate=App50/Core241+1skip. Contract: ITurnCostCalculator seam(cost-tracking M4 swaps engine), PromoteToArtifactRequest/TurnProvenance(artifact-creation M3), ITurnActionService.Retry(modelOverride), TurnMetadata.FromMessage, IClipboardService.
image-attachments: APPROVED after 1 attempt, merged into main, gate=App55/Core245+1skip. Contract: additive ChatAskContext.UserImages/ChatRequest.UserImages, IConversationService.Ask image overload, ImageAttachment, IMessageAttachmentStore, PendingTurnTokenEstimator. KNOWN PRE-EXISTING FLAKE (M1): ImageVisionCaptionTests.Caption_generation_failure_does_not_block_adding_the_image -> SQLite ObjectDisposedException race, intermittent, green on rerun. FLAG at milestone gate.
rate-limit-backoff: APPROVED after 1 attempt, merged into main, gate=App56/Core257+1skip. Contract: additive ChatFaulted.RetryAfter, Core.Ai.Backoff namespace (RetryingChatService decorator, BackoffPolicy, IJitterSource, IRetryDelay, RetryState, IRetryObserver), RetryStatusViewModel. Wired app-wide in DI. Downstream prompt-caching/v1-acceptance wrap same request path.
prompt-caching: APPROVED after 1 attempt, merged into main, gate=App56/Core266+1skip. Contract: additive CacheBreakpoint/ChatCacheSegment, ChatRequest.CacheBreakpoints, ChatRequestAssembler emits breakpoints on system+stable-resource segments.

== M2 COMPLETE: all 9 features merged into main. Final gate=App56/Core266(+1 skip @requires-network). MILESTONE GATE - awaiting human sign-off for M3. ==

fix/sqlite-pool-clear-race: FIXED M1 flake, merged into main. Root cause: 22 test Dispose() called global SqliteConnection.ClearAllPools(), disposing pooled sqlite3 handles of parallel test classes -> intermittent ObjectDisposedException. Fix: added DataStore.ClearConnectionPool() (scoped SqliteConnection.ClearPool for this store's connection string), replaced all 22 global calls. Verified 12/12 green Core loop + full gate App56/Core266+1skip. M1 flake RESOLVED - no longer a milestone-gate concern.

## M3 (in progress) base=main
artifact-creation (CONTRACT): APPROVED after 1 attempt, merged into main (0d7fbce), gate=App59/Core286+1skip. Contract: IArtifactService extended (Create/CreateFromContent/Generate/PromoteTurn/SetContent version-seam/Get/List/Rename + emit/update). ArtifactTypes registry(5 types+Normalize), ArtifactProvenance per version, real ArtifactService in DI, FakeArtifactService loud seams. Downstream: deliverable-templates->Generate; artifact-versioning owns SetContent history; edit-with-claude->version seam; report-composition->List/Get.
deliverable-templates: APPROVED after 1 attempt, merged into main (91b5b3b), gate=App60/Core303+1skip. Contract: Core.Templates namespace (ITemplateCatalog/ITemplateService + loader/assembler + default-template-catalog.json). Consumes IArtifactService.Generate. Template id recorded via prompt marker (no schema col). Downstream: edit-with-claude grounding preamble, branded-export scaffold->headings.
