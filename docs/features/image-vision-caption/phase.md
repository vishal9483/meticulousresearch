# Phase — Image Vision Caption

**SPEC:** §3.2.1. **Milestone:** M1. **Depends on:** file-upload-extraction, ai-gateway (M2)

## Goal
Add **image resources** understood via Claude's **native vision** — no OCR/vision library. Store
the original image, assemble it as an **image content block** (inline base64) at request time,
and optionally generate a **short caption cache** (one small vision call) so images are
searchable (FTS) and previewable without resending them.

> **Cross-milestone dependency:** caption generation calls `IChatService` (ai-gateway, M2). The
> add/store/preview path is M1-local and network-free; sequence caption work after ai-gateway
> lands, or ship add/store/preview first and wire captions when `IChatService` is available.
> All caption tests mock `IChatService` and remain `@unit`.

## Deliverables
1. **`IResourceService.AddImage(projectId, filePath)`** (extends the shared contract) — validate
   type (PNG/JPG/JPEG/GIF/WEBP) → copy original into resource dir → set byte_size/token_estimate
   → optionally caption → save. **No OCR/text extraction.**
2. **Vision content assembly** — a helper that, at request time, reads the stored original and
   produces an image content block (base64 inline); bytes are **not** stored inline in the DB.
3. **Optional caption cache** — when caption-on-add is enabled, one small vision call via
   `IChatService` produces a short description stored as the resource's `extracted.txt`; failure
   is non-fatal and re-runnable.
4. **Vision-capability guard** — check the selected model's `vision` flag (model catalog §6.3);
   if an image is in scope and the model can't accept images, warn + offer to switch; never
   silently drop.
5. **Image token estimate** — contribute an estimated image-token amount toward the budget
   (§3.2.1 / §3.6); coordinate the formula with `token-estimation`.
6. **Views/VMs** — image rows in the resources table; preview pane shows a thumbnail + cached
   caption; a "generate caption" action.

## Suggested design
- Reuse the file-upload blob-copy path; skip the text-extractor entirely for images.
- Keep vision-content assembly separate from persistence so M2 conversations/image-attachments
  can reuse it.
- Caption prompt is small and deterministic in tests via the mocked `IChatService` (scripted
  caption text / scripted failure).
- Read the `vision` flag from the config-driven catalog rather than hardcoding model names.

## Test-first order
1. Add-image type validation + blob-store `@unit @integration` tests → `AddImage` (no captions yet).
2. No-OCR + content-block assembly `@unit` tests → vision assembly helper.
3. Image-token contribution `@unit` test → estimate hook.
4. Caption-cache `@unit` tests (mocked `IChatService`: success/failure/disabled) → caption path.
5. Vision-capability warning `@unit` test → guard.
6. Thumbnail + caption `@ui` test → preview pane.

## Definition of done
- Add/store/preview `@unit` (+ `@integration`) scenarios green with no network; caption `@unit`
  scenarios green against a mocked `IChatService`.
- Original image stored on disk; content block assembled inline at request time (not persisted inline).
- No OCR/vision library is referenced anywhere; non-vision-model case warns and offers a switch.

## Notes for later features
- `image-attachments` (M2) reuses the vision-content assembly for images pasted into a turn.
- `full-text-search` indexes the cached caption so images are findable by description.
- `context-budget` includes image token estimates in the pre-send total.
