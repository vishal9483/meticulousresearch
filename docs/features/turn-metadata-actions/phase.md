# Phase — Turn Metadata & Actions

**SPEC:** §3.3, §3.6, §5. **Milestone:** M2. **Depends on:** streaming

## Goal
Surface per-turn metadata (model, tokens, latency, resource scope) and a per-turn cost badge
(§3.6), and provide the turn actions from §3.3: copy, retry (same/other model), edit-and-resend,
promote to artifact, and delete.

## Deliverables
1. **Turn metadata view/VM** — reads the persisted `Message` fields (model, tokens_in/out,
   cache tokens, latency_ms, resource_scope_json) and presents them inline + on expand.
2. **Per-turn cost badge** — computes/consumes `cost = tokens × catalog prices` (input, output,
   cache-read, cache-write) via the model catalog; inline badge + expandable breakdown. The
   authoritative cost engine is `cost-tracking` (M4); expose a seam so it swaps in cleanly.
3. **Turn actions**:
   - **Copy** — assistant text to clipboard.
   - **Retry (same/other model)** — regenerate the answer to the same user message; "other
     model" uses the `model-selector` per-message override.
   - **Edit-and-resend** — replace the user message text and regenerate; supersede the old
     assistant turn.
   - **Promote to artifact** — issue an artifact-creation request from the turn's content with
     provenance (source turn, model, resource scope). Artifact domain is M3; this feature only
     builds the promote request/provenance payload.
   - **Delete** — remove the turn from the conversation.
4. **Action menu UI** on each assistant turn.

## Suggested design
- Keep cost math in a small, pure helper priced from `IModelCatalog` so it is `@unit`-testable
  and later delegates to `cost-tracking` without changing the badge VM.
- Retry/edit-resend reuse the conversation `Ask` + `streaming` regenerate path; "other model"
  just passes a different model id. Preserve the original turn's resource scope on retry.
- Promote-to-artifact returns a request object (content + provenance) consumed by M3's
  `artifact-creation`; do not couple to artifact storage here.
- Delete uses the conversation service; confirm destructive delete in UI.

## Test-first order
1. Metadata `@unit` tests → metadata VM over persisted fields.
2. Cost-badge `@unit` tests (computed cost, cache itemization) → cost helper + badge VM.
3. Action `@unit` tests (copy, retry same/other, edit-resend, promote provenance, delete) →
   action commands.
4. `@ui` tests (expandable metadata, inline badge, action menu) → turn view.

## Definition of done
- Each completed turn exposes model, tokens, latency, and resource scope, and an inline cost
  badge with an expandable breakdown that includes cache tokens.
- Copy, retry (same/other model), edit-and-resend, promote-to-artifact (with provenance), and
  delete all work per §3.3.

## Notes for later features
- `cost-tracking` (M4) becomes the authoritative cost engine feeding this badge and the
  per-chat/per-project rollups; keep the badge reading through its seam.
- `artifact-creation` (M3) consumes the promote-to-artifact request built here.
- "Retry with other model" is a primary consumer of `model-selector`'s per-message override.
