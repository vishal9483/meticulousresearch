# Phase — Text Paste Resource

**SPEC:** §3.2. **Milestone:** M1. **Depends on:** projects-crud

## Goal
Introduce the **base resource model, service, and add/preview flow** using the simplest resource
type — pasted text. This feature owns the `IResourceService` contract and the resource domain
model that file-upload, URL, image, management, token-estimation, and FTS all build on. Get the
end-to-end shape right here so later types only add extraction adapters.

## Deliverables
1. **Resource domain model** matching §5 `Resource` (id, project_id, title, type, source_uri,
   blob_path, extracted_path, byte_size, token_estimate, enabled, created_at, updated_at).
2. **`IResourceService`** in Core (owned here; extended by siblings):
   `AddText(projectId, title, text)`, `Get(resourceId)`, `List(projectId)`,
   `GetExtractedText(resourceId)`. Later features add `AddFile`/`AddUrl`/`AddImage`,
   rename/re-extract/toggle/remove.
3. **Extraction storage** — write extracted text to
   `projects/{projectId}/resources/{resourceId}/extracted.txt` via `IProjectFileStore`
   (owned by data-store-migrations). Text paste has no original blob.
4. **Token-estimate hook** — call the deterministic estimator to populate `token_estimate`
   (the estimator itself is the `token-estimation` feature; here consume a simple injected
   `ITokenEstimator` so it can be swapped/refined).
5. **Views/VMs** — Resources view (table of resources: title/type/size/tokens/enabled) with an
   "Add resource → Paste text" entry, and a preview pane showing extracted text. Designed empty
   state for a project with no resources.

## Suggested design
- Define the `ResourceType` enum (`text | file | url | image | artifact_ref`) here; siblings reuse it.
- `AddText` flow: validate non-empty → create row → resolve resource dir via `IProjectFileStore`
  → write `extracted.txt` → set byte_size (UTF-8 length of text) and token_estimate → save.
- Default title = first non-empty line, trimmed, capped to a reasonable length.
- Timestamps via injected `IClock`; enabled defaults to true.
- Keep the FTS sync (indexing extracted text) to the data-store triggers; this feature just
  writes the extracted text row — `full-text-search` asserts searchability.

## Test-first order
1. Add-text validation + field `@unit` tests → domain model + `AddText`.
2. Storage `@unit @integration` tests (extracted.txt written, no blob) → `IProjectFileStore` wiring.
3. Byte-size / token-estimate `@unit` tests → sizing + estimator hook.
4. Persistence round-trip `@unit @integration` test → repository read-back.
5. Preview + table `@ui` tests → Resources view/VM.

## Definition of done
- All add/validation/field/storage `@unit` (+ `@integration`) scenarios green; `@ui` scenarios green.
- Resource model + `IResourceService` in place and consumable by sibling M1 features.
- Extracted text lands on disk at the §5 path; token estimate is populated and deterministic.

## Notes for later features
- `file-upload-extraction`, `url-resource`, `image-vision-caption` add new `Add*` methods +
  extraction adapters producing the same `extracted.txt` output.
- `resource-management` adds rename / re-extract / enable-disable / remove / token-contribution.
- `token-estimation` refines `ITokenEstimator`; `full-text-search` indexes extracted text;
  `context-budget` reads `token_estimate` and `enabled` to compute the pre-send estimate.
