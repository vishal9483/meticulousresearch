# Phase — Edit with Claude

**SPEC:** §3.4, §5. **Milestone:** M3. **Depends on:** artifact-versioning, ai-gateway

## Goal
Let the analyst refine an artifact by giving Claude a **follow-up instruction** that produces a new
version, and ensure **manual edits** also create versions. This is the iteration engine behind the
flagship flow (§9.1(5): generate a Market Research Report, iterate with Edit with Claude, compare
versions). Both edit paths funnel through the single versioning entry point owned by
`artifact-versioning`.

## Deliverables
1. **Edit-with-Claude flow** — `EditWithClaude(artifactId, instruction, model)`:
   - assembles the request = project custom instructions + enabled resources + the **current
     version content** (the artifact being edited) + the follow-up instruction;
   - calls `IChatService`/`IArtifactService` (ai-gateway); streams into a preview;
   - on completion, commits a new version via `IArtifactService.AddVersion` with created_by
     "claude", recording model, the instruction as prompt, in-scope resources, and usage/cost.
2. **Manual-edit path** — saving edited content commits a version (created_by "user", usage/cost
   0); a no-op save (unchanged content) creates no version.
3. **Streaming/cancel/failure handling** — stream to a preview; commit a version only on
   successful completion; cancel and error commit nothing and leave the current version intact.
4. **Views/VMs**: "Edit with Claude" instruction bar in the artifact editor with a per-edit model
   selector; streaming preview; commit/keep vs. discard.

## Suggested design
- Reuse the ai-gateway generation contract; in tests, `FakeChatService` replays deterministic
  token streams, usage, and error codes (TESTING-STRATEGY §4) — no live API.
- The follow-up request must include the current version content as the edit target so Claude
  revises rather than regenerates from scratch; grounding uses **enabled** resources only,
  consistent with deliverable-templates.
- Do **not** re-implement version creation here — call `artifact-versioning.AddVersion`. This keeps
  immutability, ordering, and provenance in one place.
- Detect no-op manual saves by comparing normalized content to the current version before adding.
- Reuse rate-limit backoff and prompt caching from ai-gateway (M2) transparently.

## Test-first order
1. Follow-up-creates-version + context/grounding `@unit` tests → `EditWithClaude` request assembly.
2. Provenance/usage + per-edit model `@unit` tests → version metadata capture.
3. Manual-edit + no-op-save `@unit` tests → manual path + change detection.
4. Streaming/cancel/failure `@unit` tests → commit-on-complete semantics.
5. Iterate-flagship `@unit` test → version chain + diffable across versions.
6. `@ui` test (Edit-with-Claude prompt bar) → editor bar + model selector.

## Definition of done
- A follow-up instruction creates a Claude-authored version recording model/prompt/in-scope
  resources/usage; the prior version is unchanged.
- Manual edits create user-authored versions; no-op saves create none.
- Cancel and failure create no version and leave the current version intact.
- Iterating a Market Research Report yields an ordered version chain that `artifact-diff` can
  compare (satisfying the §9.1(5) iterate-and-compare bar) — all via FakeChatService in tests.

## Notes for later features
- Pairs directly with `artifact-diff` (review a Claude edit before keeping it) and `artifact-
  versioning` (revert if the edit is worse).
- `cost-tracking` (M4) reads the usage this feature records on Claude-authored versions.
