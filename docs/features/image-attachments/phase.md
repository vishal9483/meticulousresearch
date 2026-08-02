# Phase — Image Attachments (in-thread)

**SPEC:** §3.2.1, §3.6, §6.3. **Milestone:** M2. **Depends on:** conversations, image-vision-caption

## Goal
Let the analyst paste or attach an image **directly into a conversation turn** (not only as a
persistent resource), render it inline as a thumbnail, send it as a vision content block, and
count its tokens toward the context budget and cost. Reuses the vision handling from
`image-vision-caption` (M1) but scopes the image to message content, not project resources.

## Deliverables
1. **Composer attachments** — the composer accepts pasted images and attached image files
   (PNG/JPG/JPEG/GIF/WEBP); a pending turn carries text + zero-or-more image attachments;
   attachments are removable before send.
2. **Message content model** — extend the user `Message` to carry image content (stored as
   message content, e.g. under the conversation's message blobs), **not** as a `Resource`.
3. **Request assembly** — the grounding assembler emits the user turn as multimodal content:
   text block + one image content block per attachment (same vision-block shape as
   `builtin-file-tools-sandbox` Read and `image-vision-caption`).
4. **Inline thumbnails** — render attachments as thumbnails in the composer and in the sent
   user turn; click opens a larger preview.
5. **Token/cost** — pre-send estimate includes an estimated image token contribution (labeled
   "estimated", §3.6); recorded input tokens/cost use the backend's authoritative usage
   (which includes image tokens).
6. **Vision guard** — when the selected model has `vision=false` (per `model-selector`), warn on
   attach and offer to switch to a vision-capable model.

## Suggested design
- Reuse the M1 image pipeline (base64/inline reference assembly, thumbnail generation) rather
  than duplicating it; the difference is lifetime/scope (per-turn vs. persistent resource).
- Store the original image bytes with the message so a re-open still shows the thumbnail and a
  retry can re-send it; do not register a `Resource` row.
- Image token estimate is local + labeled "estimated" (§3.6 provenance rule); authoritative
  counts come from the backend usage fields via `ai-gateway`.
- Vision warning shares the advisory (non-blocking) mechanism from `model-selector`.
- Keep attachment/assembly logic in Core/VM for `@unit` tests; `@ui` covers thumbnail + preview.

## Test-first order
1. Attach/remove + "not a resource" `@unit` tests → composer attachment model.
2. Request-assembly `@unit` tests (image content block(s) alongside text) → multimodal assembly.
3. Token/cost `@unit` tests (recorded input includes image; estimate labeled) → estimate + usage.
4. Vision-guard `@unit` test → warn/switch hook.
5. `@ui` tests (inline thumbnails, larger preview) → composer/turn views.

## Definition of done
- Images can be pasted/attached to a turn, removed before send, and are sent as vision content
  blocks alongside the text; multiple images per turn supported.
- Attachments are stored as message content and never create project resources.
- Thumbnails render inline in composer and turn; clicking opens a larger preview.
- Image tokens are included in recorded input tokens/cost; pre-send estimate includes an
  "estimated" image contribution; non-vision model warns and offers a switch.

## Notes for later features
- `cost-tracking` (M4) rolls per-turn cost (image tokens included) into chat/project totals.
- `context-budget` (M1) consumes the image token estimate when checking the pre-send budget.
- Shares the vision-block shape with `image-vision-caption` and `builtin-file-tools-sandbox`.
