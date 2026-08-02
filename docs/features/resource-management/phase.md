# Phase — Resource Management

**SPEC:** §3.2. **Milestone:** M1. **Depends on:** text-paste-resource

## Goal
Complete the resource lifecycle: **rename, re-extract, enable/disable, remove, preview extracted
text, and show token-estimate contribution** — the controls that let an analyst curate exactly
what grounds their work.

## Deliverables
1. **`IResourceService` management methods** (extend the shared contract):
   `Rename`, `SetEnabled`, `ReExtract`, `Remove`, plus `GetExtractedText` for preview and a
   scope helper `ListEnabled(projectId)` used by context assembly.
2. **Re-extract** — re-run the appropriate extractor against the stored original (file/url),
   refresh `extracted.txt`, recompute `token_estimate`, update status; unavailable for text paste.
3. **Enable/disable** — toggle `enabled`; enabled state is the single source of truth for what
   the generation-context assembler includes.
4. **Remove** — delete the DB row (FTS rows follow via triggers) and the resource directory on
   disk; confirmation in the UI.
5. **Token-estimate contribution** — per-row estimate in the table and an **enabled-scope total**
   (disabled resources excluded).
6. **Preview pane** — extracted text + metadata (type, byte size, token estimate); image preview
   handled by `image-vision-caption`.

## Suggested design
- Re-extract reuses the extractors from `file-upload-extraction` and the fetch/convert path from
  `url-resource` (resolve by resource type); keep it idempotent.
- The enabled-scope total is derived (sum of `token_estimate` where `enabled`), computed in the
  view-model so it's `@unit`-testable and reused by `context-budget`.
- Remove deletes files via `IProjectFileStore`; ensure the row and FTS index are cleaned.
- Timestamps via `IClock`; renames update `updated_at`.

## Test-first order
1. Rename validation `@unit` tests → `Rename`.
2. Enable/disable + scope-inclusion `@unit` tests → `SetEnabled` + scope helper.
3. Re-extract (refresh / recover-failed / not-for-text) `@unit` tests → `ReExtract`.
4. Token-contribution + enabled-total `@unit` tests → table VM.
5. Remove `@unit @integration` test → row+dir deletion.
6. Toggle / preview / confirm-remove `@ui` tests → Resources view wiring.

## Definition of done
- All management `@unit` (+ `@integration`) scenarios green; `@ui` scenarios green.
- Enabled state drives generation scope; enabled-scope token total is correct.
- Remove deletes both row and on-disk files; re-extract refreshes text + estimate.

## Notes for later features
- `context-budget` consumes `ListEnabled` + the enabled-scope total for the pre-send estimate.
- `conversations` (M2) reads enabled resources for grounding; the resource-scope chips reflect
  the same enabled set.
- `full-text-search` index stays in sync through the data-store triggers on rename/re-extract/remove.
