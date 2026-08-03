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
