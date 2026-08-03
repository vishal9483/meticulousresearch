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
artifact-versioning: APPROVED after 1 attempt, merged into main (b485497), gate=App60/Core321+1skip. Contract: IArtifactService version-history surface (AddVersion immutable-write path, GetHistory, SetCurrentVersion, RevertTo, DuplicateArtifact, DeleteArtifact/DeleteVersion w/ current-version guard, Regenerate via ITurnCostCalculator 4th ctor param, OverwriteVersionContent immutability seam, PromoteToResource->artifact_ref Resource). Downstream: artifact-diff->GetHistory; edit-with-claude/manual->AddVersion; report-composition/branded-export read current version.
artifact-diff: APPROVED after 1 attempt, merged into main (d87e44e), gate=App60/Core328+1skip. Contract: Core.Artifacts.Diff namespace, IArtifactDiffService.Diff(base,compare)->ArtifactDiff (text DiffSegments or TableDiff, csv-routed on ContentFormat). Pure/read-only, consumes GetHistory only. Downstream: edit-with-claude reuses to review a Claude edit before keep.
edit-with-claude: APPROVED after 1 attempt, merged into main (e708b94), gate=App63/Core340+1skip. No new contract. IEditWithClaudeService (Core.Artifacts) consumes existing only: kept version->IArtifactService.AddVersion, Claude call->IChatService, grounding->IResourceService.ListEnabled, instructions->IProjectService, cost->ITurnCostCalculator seam. Pairs with artifact-diff; read by cost-tracking M4.
report-composition: APPROVED after 1 attempt, merged into main (0706989), gate=App67/Core352+1skip. New IReportCompositionService (Core.Reports) consumes IArtifactService only. Composition = doc artifact w/ JSON manifest (kind=report-composition + ordered section refs + optional pinnedVersionId). Render()->CompiledReport (ordered RenderedSections + concatenated markdown, CSV->table, broken-ref placeholders). Downstream: branded-export (M4) consumes.

== M3 COMPLETE: all 6 features merged into main. Final gate=App67/Core352(+1 skip @requires-key). Contract=artifact-creation. NOTE: intermittent M1 SQLite pool flake (ImageVisionCaption) still observed once, green on rerun. MILESTONE GATE - awaiting human sign-off for M4. ==

## M4 (in progress) base=main
branded-export: APPROVED after 1 attempt, merged into main (3ee2b1f), gate=App67/Core382+1skip. New IExportService (Core.Export): Preview/Export->ExportResult over ExportSource/ExportArtifact, RenderedDocument tree, deterministic md/docx/xlsx/pdf writers (sorted zip entries, explicit rel IDs, frozen timestamps), BrandSettings input record (Unset->navy; NOT yet persisted-a future settings integration wires accent/logo/confidentiality). Consumes report-composition/artifact services. Downstream v1-acceptance.
cost-tracking: APPROVED after 1 attempt, merged into main (659bcce feat), gate=App67/Core405+1skip. Real cost engine behind existing ITurnCostCalculator seam (left intact-CatalogTurnCostCalculator). New ICostService.GetPricedTurns->PricedTurnRecord (TurnId, CostSource, conv/artifact id, model, TurnUsage tokens, Cost null=unknown-price, UnknownPrice, IsAuthoritative, Timestamp, audit-only SnapshotCostUsd). Cost ALWAYS recomputed from stored tokens at current catalog prices (no persisted authoritative cost). Prices via ICostPriceSource (CatalogCostPriceSource adapts IModelCatalog.GetPrice; cache rates=Anthropic 0.1x/1.25x). Downstream usage-csv-export consumes GetPricedTurns. NOTE: intermittent M1 SQLite pool flake reappeared once post-merge, green on rerun.
usage-csv-export: APPROVED after 1 attempt, merged into main (3154efc feat), gate=App67/Core415+1skip. New Core IUsageCsvExporter (Render/Export) - pure serializer over ICostService.GetPricedTurns, recomputes nothing. Columns timestamp,source,model,tokens_in,tokens_out,cost_usd; ascending timestamp (tie-break TurnId ordinal), RFC-4180 escaping, invariant, CRLF, UTF-8 no-BOM -> byte-identical repeats; empty->header-only. DI-registered; dashboard ExportUsageCsvCommand.
backup-restore: APPROVED after 1 attempt, merged into main (045cbb2 feat), gate=App67/Core429+1skip. New Core IProjectBackupService (Backup/Restore) + RestoreConflictPolicy + manifest/data/exception types, DI-registered. Zip = manifest.json (format+schema version, project id) + data.json (six-table subset, tokens verbatim) + files/resources/{id}/. Restore transactional, remaps ids on restore-as-copy, refuses newer-schema/corrupt archives, NEVER includes vault/env secrets or other projects' rows. v1-acceptance drives round-trip.

== M4 COMPLETE: all 4 features (branded-export, cost-tracking, usage-csv-export, backup-restore) merged into main. Final gate=App67/Core429(+1 skip @requires-key). NOTE: intermittent M1 SQLite pool flake observed once during cost-tracking post-merge, green on rerun. MILESTONE GATE - awaiting human sign-off for M5. ==
