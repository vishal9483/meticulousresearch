# Phase — File Upload & Extraction

**SPEC:** §3.2. **Milestone:** M1. **Depends on:** text-paste-resource

## Goal
Add the **file resource type and the extraction pipeline**: accept PDF/DOCX/TXT/MD/CSV/XLSX,
store the original blob **and** the extracted text, capturing lightweight structure (tables,
sheets). Extraction runs async with progress and degrades gracefully on failure.

## Deliverables
1. **`IResourceService.AddFile(projectId, filePath)`** (extends the contract owned by
   text-paste-resource) — copies the original into the resource dir, runs extraction, populates
   fields, saves.
2. **Extraction pipeline** — an `ITextExtractor` per format resolved by extension/content:
   - PDF (text layer), DOCX, TXT/MD (passthrough), CSV, XLSX (per-sheet, tabular text).
   - Produces plain text + lightweight structure; writes `extracted.txt`.
3. **Blob storage** — copy original to
   `projects/{projectId}/resources/{resourceId}/original.{ext}` via `IProjectFileStore`; set
   `source_uri` (original name/path), `blob_path`, `byte_size`.
4. **Extraction status** — success / failed / empty, with a human-readable reason for failures;
   surfaced so `resource-management` can offer re-extract (SPEC §3.7 error states).
5. **Async + progress** — extraction off the UI thread with a progress indicator; drag-and-drop
   onto the Resources view; unsupported-type rejection.

## Suggested design
- Reuse the text-paste storage/sizing/estimate flow; only the extraction step differs by adapter.
- Keep extractor libraries behind `ITextExtractor` so they're swappable and unit-testable with
  small fixture files (checked into the test project, not `%LOCALAPPDATA%`).
- CSV/XLSX: emit a readable tabular representation (header + rows; sheet name headings for XLSX)
  so it's useful for grounding and indexes well in FTS.
- Scanned/text-less PDF → empty extracted text + hint toward `image-vision-caption`; do not fail.
- Corrupt file → still store the blob, mark status failed, keep a recovery path (re-extract).
- Estimation via the shared `ITokenEstimator`; timestamps via `IClock`.

## Test-first order
1. Supported-type extraction `@unit @integration` outline (per ext) → adapters + `AddFile`.
2. Tabular/multi-sheet `@unit` tests → CSV/XLSX structure extraction.
3. Blob-copy + fields `@unit @integration` tests → storage + sizing.
4. Unsupported / corrupt / scanned `@unit` tests → validation + failure/empty status.
5. Progress + drag-drop `@ui` tests → Resources view wiring.

## Definition of done
- All extraction, fields, and failure `@unit` (+ `@integration`) scenarios green; `@ui` green.
- Every supported type stores both original blob and extracted text at the §5 paths.
- Failures never crash: blob retained, status set, re-extract offered.

## Notes for later features
- `image-vision-caption` handles image types (a different pipeline — vision, no OCR); a
  scanned-PDF hint points there.
- `resource-management` wires the "re-extract" action to the same extractor.
- `token-estimation` / `full-text-search` / `context-budget` consume the extracted text and
  token estimate produced here, identically to text resources.
