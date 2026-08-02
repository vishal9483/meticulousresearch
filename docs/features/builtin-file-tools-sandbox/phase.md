# Phase — Built-in File Tools (Sandboxed)

**SPEC:** §7.4, §3.4, §3.2.1. **Milestone:** M2. **Depends on:** ai-gateway

## Goal
Give the model a **fixed, curated** tool set (Glob, Grep, Read, Edit, Write + emit_artifact /
update_artifact) confined to the active project's directory tree. Not a user-extensible
tool/MCP marketplace — the set is closed. Path traversal outside `projects/{projectId}` is
rejected, and every tool call is logged and visible in the conversation.

## Deliverables
1. **Curated tool registry** — exposes exactly the seven tools to the model loop and nothing
   else. Wired into the Agent SDK sidecar loop (primary) and provided consistently for the
   direct-API path where applicable.
2. **File/search tools** (project-scoped):
   - `Glob` — pattern match within the project's resources/artifacts dirs.
   - `Grep` — content search across resource extracted text and artifact version content.
   - `Read` — resource extracted text or artifact version; **images returned as vision content
     blocks** (§3.2.1), not raw bytes-as-text.
3. **Authoring tools** — `Edit` / `Write` route through `IArtifactService` so results land as
   **new artifact versions** (never silent overwrite; §3.4 versioning applies).
4. **Artifact tools** — `emit_artifact` / `update_artifact` map to the structured
   `IArtifactService` create/update contract.
5. **Sandbox guard** — a single path-resolution/validation layer that resolves every tool path
   under `projects/{projectId}` and rejects anything escaping it (`..`, absolute paths, other
   projects, `db.sqlite`, app files) with a sandbox-violation error.
6. **Tool-call log** — record each call (name, inputs, outcome) on the turn and surface it inline
   in the thread for transparency.

## Suggested design
- **Sandbox first.** The guard is the security boundary; implement and test it before the tools
  so no tool can be built that bypasses it. Canonicalize paths and verify the resolved path is a
  descendant of the project root; reject symlink escapes.
- Route `Edit`/`Write`/`emit_artifact`/`update_artifact` exclusively through `IArtifactService`
  (owned by `ai-gateway`, realized by M3) so versioning is enforced centrally.
- `Read` on images produces the same vision content block shape the grounding assembler uses
  (§3.2.1); keep that shared.
- Tool set is data-driven but **closed**: no registration API surfaced to users/config.
- Use a temp project dir per test (TESTING-STRATEGY §4); drive tool calls via `FakeChatService`
  scripted tool-use turns.

## Test-first order
1. Sandbox-guard `@unit @integration` tests (traversal, other project, db/app files) → path guard.
2. Tool-set exposure `@unit` test (exactly seven, nothing else) → registry.
3. Glob/Grep/Read (+ image vision block) `@unit @integration` tests → read/search tools.
4. Edit/Write + emit/update `@unit @integration` tests (new versions, no overwrite) → authoring
   tools over `IArtifactService`.
5. Transparency `@unit` + `@ui` tests → tool-call logging + inline display.

## Definition of done
- Exactly the curated seven tools are available; no others.
- Every tool is confined to `projects/{projectId}`; all traversal/other-project/db/app-file
  attempts are rejected and leave those locations untouched.
- Edit/Write/emit/update create new artifact versions via the artifact service (no silent
  overwrite); Read returns images as vision blocks.
- All tool calls are logged and visible in the conversation.

## Notes for later features
- M3 `artifact-creation`/`artifact-versioning` realize `IArtifactService`; the write/emit paths
  here must produce versions consistent with that.
- `image-attachments` and `image-vision-caption` share the vision-content-block shape used by Read.
- The tool-call log is a transparency source `turn-metadata-actions` and later provenance reuse.
