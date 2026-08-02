# Phase — Artifact Creation

**SPEC:** §3.4, §5, §7.4. **Milestone:** M3. **Depends on:** conversations

## Goal
Introduce the **artifact domain** — the substantial, standalone, editable research outputs that
the whole M3 milestone builds on. This feature **owns** the `Artifact`/`ArtifactVersion` model,
the `IArtifactService` basics, and the four creation paths (promote a turn, generate directly,
generate from a template seam, create blank). Later M3 features (templates, versioning, diff,
edit-with-claude, report-composition) extend this domain; they do not redefine it.

## Deliverables
1. **`IArtifactService`** in Core (owned here):
   - `Create(projectId, type, title)` — blank artifact + empty first version.
   - `CreateFromContent(projectId, type, title, content, format, provenance)` — used by promote.
   - `Generate(projectId, request)` — direct generation via `IChatService`/`IArtifactService`
     from ai-gateway; persists the emitted content as version 1.
   - `PromoteTurn(turnId, title)` — build an artifact from an assistant turn.
   - `SetContent(artifactId, content)` — manual save (creates a version; the version *machinery*
     is fleshed out by `artifact-versioning`, but the seam is defined here).
   - `Get`, `List(projectId)`, `Rename`.
2. **Domain model** matching §5 `Artifact` (id, project_id, title, type, current_version_id,
   created/updated) and `ArtifactVersion` (version_no, content, content_format, model, prompt,
   tokens_in/out, cost_usd, resource_scope_json, created_by, created_at).
3. **Type registry** — the five v1 types with their default `content_format`
   (doc→markdown, text→text, code→code, table→csv, diagram→mermaid) and validation of unknown types.
4. **`emit_artifact` / `update_artifact` contract handler** (§7.4) — the structured tool call the
   Agent SDK loop uses; validates required fields and routes writes through `IArtifactService`
   (never a silent file overwrite — §3.4/§7.4).
5. **Views/VMs**: Artifacts list (designed empty state), New-artifact flow (prompt + model +
   resource scope; blank option), artifact editor shell (content editor; live preview for
   md/diagram/table wired minimally). Promote action on conversation turns.
6. **FTS wiring** — artifact version content indexed for project search (schema owned by
   data-store-migrations; this feature ensures new content flows into it).

## Suggested design
- Reuse `IChatService`/`IArtifactService` generation contract from ai-gateway; AI calls in tests
  go through `FakeChatService` (deterministic streams + scripted `emit_artifact`), per
  TESTING-STRATEGY §4.
- **Provenance carries onto every version**: model, prompt, resource_scope_json, created_by.
  Promoted turns copy the turn's model/usage/resource scope; generated artifacts copy the
  request's; blank/manual versions set created_by "user", usage 0, model/prompt null.
- Keep the version-creation entry point (`SetContent`) minimal here and let `artifact-versioning`
  own history semantics (immutability, ordering, current/revert/duplicate/delete). Define the
  seam so those features slot in without reshaping the model.
- Timestamps via injected `IClock`.
- Diagram preview renders Mermaid source to SVG at view time; the stored artifact is always the
  raw Mermaid source, never a rendered image.

## Test-first order
1. Type registry + create/blank `@unit` tests → domain model + `Create`.
2. Promote-turn `@unit` tests → `PromoteTurn` + provenance copy.
3. Direct-generation `@unit` tests → `Generate` over FakeChatService + usage capture.
4. `emit_artifact`/`update_artifact` contract `@unit` tests → tool handler + validation.
5. Persistence/schema/FTS `@unit @integration` tests → mapping + search wiring.
6. `@ui` tests (editor opens on generate, promote action present, empty state) → views.

## Definition of done
- All five types create with correct `content_format`; unknown types rejected.
- All four creation paths produce an artifact with a version-1 record and correct provenance.
- Generation uses FakeChatService in tests (no live API); usage tokens captured on versions.
- `emit_artifact` writes route through the artifact service; malformed calls rejected.
- Artifact content is FTS-searchable; Artifacts view has a designed empty state.

## Notes for later features
- `deliverable-templates` builds the template gallery + prompt scaffolds on top of `Generate`.
- `artifact-versioning` takes over version history semantics (immutable ordered history,
  set-current/revert/duplicate/delete/promote-to-resource) using the model defined here.
- `edit-with-claude` and manual edit both create versions via the seam defined here.
- `report-composition` composes multiple artifacts; it consumes this domain, not extends it.
- `branded-export` (M4) exports the current version / composed order.
- `cost-tracking` (M4) reads tokens_in/out persisted on versions here.
