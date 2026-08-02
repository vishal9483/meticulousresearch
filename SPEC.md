# MeticulousResearch Desktop — Specification

> A local-first Windows desktop application, modeled on the Claude Desktop app and
> Claude Projects, with a **deliberately reduced scope**: its center of gravity is
> **creating and managing research projects** — their resources, their artifacts, and
> model-selectable Q&A — rather than being a general-purpose chat client.

- **Status:** Draft v1 (spec only — no code yet)
- **Date:** 2026-08-03
- **Platform:** Windows 11 (x64/arm64)
- **UI stack:** .NET 8 + WPF (C#)
- **AI backend:** Claude Agent SDK (run as a sidecar process — see §7)
- **Storage:** Local-only — SQLite (metadata) + files on disk (blobs)

---

## 1. Product summary

### 1.1 What it is
A single-user desktop app for analysts to run **research projects**. Each project is a
self-contained workspace bundling:

- **Resources** — the source material Claude should ground its answers in (pasted text,
  uploaded files, URLs).
- **Conversations** — model-selectable Q&A threads scoped to the project, grounded in its
  resources.
- **Artifacts** — substantial, standalone outputs (documents, tables, code, diagrams) that
  Claude produces, which the user curates, versions, and exports.

### 1.2 What it is *not* (reduced capabilities vs. Claude Desktop)
Explicitly **out of scope** for v1, to keep focus:

- No account system, cloud sync, or multi-user collaboration/sharing.
- No general (project-less) chat — every conversation lives inside a project.
- No arbitrary/user-extensible tool use, computer use, or MCP marketplace. (A **fixed,
  built-in tool set** — file read/search/edit/write plus artifact emit — backs grounding and
  artifact creation; see §7.4.)
- No image *generation*; no voice; no browser automation.
  (Image *input* **is** supported — Claude reads images via its vision capability; see §3.2.)
- No code *execution* of generated artifacts (they are authored and exported, not run).
- No mobile/web/cross-platform builds.

### 1.3 Design principles
1. **Local-first & private** — all data on the user's machine; the only network egress is
   to the Anthropic API via the Agent SDK.
2. **Project as the unit of work** — everything hangs off a project.
3. **Provenance everywhere** — every artifact records the model, resources, and prompt that
   produced it.
4. **Nothing is lost** — artifacts and conversations are versioned; edits never destroy
   prior state.

---

## 2. Personas & primary use cases

**Persona: the analyst.** Produces research memos, briefs, and structured summaries from a
pile of source material, iterating with Claude.

Primary flows:
1. Create a project, set custom instructions.
2. Add resources (paste notes, drop in PDFs/DOCX, add URLs).
3. Ask questions in a conversation, picking a model per the task (fast vs. deep).
4. Promote a good answer — or ask Claude to draft — into an **artifact**.
5. Iterate on the artifact via follow-ups; compare versions; export to MD/DOCX/PDF/XLSX.

---

## 3. Feature specification

### 3.1 Projects
- **CRUD:** create, rename, duplicate, archive, delete a project.
- **Fields:** name, description, custom instructions (system-prompt text), default model,
  created/updated timestamps, color/emoji tag (optional), archived flag.
- **Custom instructions** — free-text, injected into the system prompt for every
  conversation and generation in the project (tone, role, output conventions).
- **Project dashboard** — resource count, artifact count, last activity, quick actions
  (New conversation / Add resource / New artifact).
- **Search** — filter projects by name/description; full-text search within a project across
  resources, conversations, and artifacts (SQLite FTS5).

### 3.2 Resources (project knowledge)
- **Types:**
  - *Text paste* — arbitrary text captured inline.
  - *File upload* — PDF, DOCX, TXT, MD, CSV, XLSX.
  - *Image* — PNG, JPG/JPEG, GIF, WEBP. **Supported from v1.** Images are understood via
    **Claude's native vision capability** (the raw image is sent to the model as an image
    content block) — the app does **not** use a separate OCR/vision library. See §3.2.1.
  - *URL* — fetched and converted to text/markdown at add-time; original URL retained.
  - *Promoted artifact* — an artifact re-used as a resource in the same or another project.
- **Per resource:** title, type, source path/URL, extracted text (or vision caption for
  images), byte size, token estimate, enabled/disabled toggle, added timestamp.
- **Extraction pipeline** — on add, extract plain text + lightweight structure; store both
  the original blob (in the project's files dir) and the extracted text (for grounding).
- **Selection model** — each conversation/generation includes *enabled* resources up to a
  configurable context budget; user can toggle which resources are in scope. When resources
  exceed the budget, the app warns and (v1) lets the user deselect; (future) summarize/RAG.
- **Management:** rename, re-extract, enable/disable, remove, preview extracted text, see
  token estimate contribution.

#### 3.2.1 Image handling (vision, from v1)
- **No OCR/vision library.** Instead of Tesseract or a cloud vision API, image resources are
  passed to Claude as **image content blocks** on the request, and the model's own vision
  reads them. This keeps dependencies minimal and quality high.
- **Storage:** the original image is stored in the project's files dir; a base64/inline
  reference is assembled at request time for in-scope image resources.
- **Optional caption cache:** on add, the app may make one small vision call to generate a
  short text caption/description, stored as the resource's "extracted text" so image
  resources are searchable (FTS) and previewable without re-sending the image. This is a
  convenience index only — the actual image is still sent to the model at generation time.
- **Model requirement:** all default-tier models in §6 accept image input; if a selected
  model does not, the app warns and offers to switch. Image tokens count toward the context
  budget and cost (§3.6).
- **Multimodal messages:** users can also paste/attach an image directly into a conversation
  turn (not only as a persistent resource).

### 3.3 Conversations & model-selectable Q&A
- **Scope:** every conversation belongs to exactly one project.
- **Model selector** — per-conversation and overridable per-message. Presented as friendly
  tiers mapped to concrete model IDs (see §6). Shows the model used on each assistant turn.
- **Streaming** — assistant responses stream token-by-token (Agent SDK streaming → UI).
- **Grounding** — the request is assembled from: project custom instructions + enabled
  resources (or references) + conversation history + the user's message.
- **Turn metadata** — model, token usage (in/out), latency, and which resources were in
  scope are recorded per assistant turn.
- **Actions on a turn:** copy, retry (same/other model), edit-and-resend, **promote to
  artifact**, delete.
- **Stop/cancel** a streaming generation.

### 3.4 Artifacts
- **Definition:** a substantial, standalone, editable output. Mirrors Claude Artifacts.
- **Types (v1):**
  - Markdown / rich text document
  - Plain text
  - Code snippet (syntax-highlighted; not executed)
  - Table / dataset (backed by CSV/rows; exports to XLSX)
  - Diagram (Mermaid source rendered to preview/SVG)
- **Creation paths:**
  1. *Promote* an assistant turn to a new artifact.
  2. *Generate* directly ("New artifact" → prompt + model + resources).
  3. *Create blank* and edit manually.
- **Versioning** — every follow-up edit or regeneration creates a **new immutable version**;
  the artifact keeps an ordered version history. Each version records the model, prompt,
  in-scope resources, and timestamp.
- **Diff** — side-by-side / inline diff between any two versions.
- **Editing** — direct manual edit (creates a version) *or* "Edit with Claude" (follow-up
  instruction creates a version).
- **Management:** rename, set current version, revert to a version, duplicate, delete,
  promote-to-resource.
- **Export** — Markdown, DOCX, PDF, and (for tables) XLSX. Export uses the current version.

### 3.5 Global / cross-cutting
- **Settings** — API key entry (stored via Windows DPACI/credential vault, not plaintext),
  default model, context budget, data directory location, theme (light/dark/system),
  telemetry off by default.
- **Command palette** (Ctrl+K) — jump to project, new conversation, new artifact, search.
- **Keyboard shortcuts** — new project/conversation/artifact, send (Ctrl+Enter), stop (Esc),
  search (Ctrl+K).
- **Offline behavior** — browsing/editing existing data works offline; generation requires
  network and a valid key (clear error state otherwise).

### 3.6 Cost tracking & usage metering
Every model call has a measurable cost; the app surfaces it at three levels so analysts can
see what a piece of work costs.

- **Per turn** — each assistant turn shows input tokens, output tokens, and computed cost
  (small inline badge; full breakdown on hover/expand). Image and cache tokens are included.
- **Per conversation (chat)** — a running total for the conversation: total tokens and total
  cost across all turns, shown in the conversation header. Updated live as turns complete.
- **Per project (consolidated)** — the project dashboard shows **consolidated cost** across
  *all* conversations **and** artifact generations in the project, with a breakdown by:
  - conversations vs. artifact generation,
  - model (how much was spent on each model tier),
  - time window (e.g. today / this week / all-time).
- **Cost computation** — `cost = input_tokens × input_price + output_tokens × output_price`
  (plus cache-read/cache-write rates when prompt caching is used). **Per-model prices live in
  the config-driven model catalog** (§6.3) so they update without a rebuild. Prices are in
  USD per million tokens (MTok); the app stores the *tokens* as ground truth and computes
  cost from current prices, so a price-table update reprices historical usage consistently.
- **Provenance:** token counts come from the Agent SDK / API usage fields returned on each
  response (authoritative), not from local estimation. Pre-send estimates (§3.2 budget) use
  local token estimation and are labeled "estimated."
- **Export:** project cost/usage is exportable as CSV (per-turn rows) for reporting.
- **Optional guardrails (config):** a soft per-project monthly budget that, when exceeded,
  shows a warning banner (does not hard-block). Off by default.

---

## 4. UX / screen inventory

1. **Home / Projects list** — grid or list of project cards; create/search; archived toggle.
2. **Project workspace** — three-pane layout:
   - *Left:* project nav (Conversations, Resources, Artifacts, Settings).
   - *Center:* active view (a conversation thread, a resource preview, or an artifact editor).
   - *Right (contextual):* resource scope panel during chat; version history during artifact
     editing.
3. **Conversation view** — message thread, model selector, resource-scope chips, streaming
   composer, per-turn actions, **per-turn cost badge and a running chat-cost total in the
   header** (§3.6). Image attachments render as thumbnails inline.
4. **Resources view** — table of resources with type/size/tokens/enabled; add menu (paste /
   file / image / URL); preview pane (image resources show a thumbnail + cached caption).
5. **Artifact editor** — content editor + live preview (for MD/diagram/table), version
   history rail, diff mode, export menu, "Edit with Claude" prompt bar.
6. **Project dashboard** — resource/artifact/conversation counts, last activity, and the
   **consolidated cost panel** (§3.6): total spend with breakdowns by model, by
   conversations-vs-artifacts, and by time window; CSV export.
7. **Settings** — API key, defaults, storage, appearance, model catalog & pricing, optional
   per-project budget.

Visual language: clean, Claude-Desktop-like; light/dark themes; WPF with a modern control
library (see §7.1).

---

## 5. Data model

SQLite for metadata + a per-project files directory for blobs and extracted text.

```
Project(
  id, name, description, custom_instructions, default_model,
  color, archived, created_at, updated_at)

Resource(
  id, project_id, title, type,            -- text | file | url | artifact_ref
  source_uri,                             -- original path/URL (nullable)
  blob_path,                              -- stored original (nullable)
  extracted_path,                         -- extracted text file
  byte_size, token_estimate, enabled,
  created_at, updated_at)

Conversation(
  id, project_id, title, model_default, created_at, updated_at)

Message(
  id, conversation_id, role,              -- system | user | assistant
  content, model, tokens_in, tokens_out,
  tokens_cache_read, tokens_cache_write,  -- for prompt-caching cost
  cost_usd,                               -- computed at turn completion (snapshot)
  latency_ms, resource_scope_json,        -- ids of resources in scope
  created_at)

Artifact(
  id, project_id, title, type,            -- doc | text | code | table | diagram
  current_version_id, created_at, updated_at)

ArtifactVersion(
  id, artifact_id, version_no, content,
  content_format, model, prompt,
  tokens_in, tokens_out, cost_usd,        -- usage for generated versions (0 for manual edits)
  resource_scope_json, created_by,        -- user | claude
  created_at)

Setting(key, value)   -- app-level; secrets go to Credential vault, not here
```

Full-text search via FTS5 virtual tables over resource extracted text, message content, and
artifact version content.

**Files on disk** (default `%LOCALAPPDATA%/MeticulousResearch/`):
```
/db.sqlite
/projects/{projectId}/resources/{resourceId}/original.{ext}
/projects/{projectId}/resources/{resourceId}/extracted.txt
/exports/...            (transient)
/logs/...
```

---

## 6. Model selection

The UI exposes friendly tiers; each maps to a concrete Claude model ID, editable in
Settings so IDs can be updated without a new build. Model IDs below are the current
Anthropic Claude API IDs as of 2026-08-03 (source: Anthropic models overview, see §6.3).

### 6.1 Default tiers (shown in the model picker)

| Tier (UI)        | Model                | Claude API ID              | Context | Max out | When to use                                    |
|------------------|----------------------|----------------------------|---------|---------|------------------------------------------------|
| **Frontier**     | Claude Fable 5       | `claude-fable-5`           | 1M      | 128k    | Highest capability; long-running agentic synthesis |
| **Deep**         | Claude Opus 5        | `claude-opus-5`            | 1M      | 128k    | Complex research synthesis, enterprise work     |
| **Balanced**     | Claude Sonnet 5      | `claude-sonnet-5`          | 1M      | 128k    | Everyday drafting and Q&A (best speed/intel)    |
| **Fast**         | Claude Haiku 4.5     | `claude-haiku-4-5`         | 200k    | 64k     | Quick lookups, cheap iterations                 |

- **Default project model:** Claude Opus 5 (`claude-opus-5`).
- Selectable per conversation and per message; recorded on every turn and artifact version.
- Model list is config-driven (a JSON mapping) so new models can be added without a rebuild.

### 6.2 Also available (selectable, not default tiers)

Exposed in an "All models" dropdown for users who want them:

| Model            | Claude API ID              | Context | Max out | Notes                              |
|------------------|----------------------------|---------|---------|------------------------------------|
| Claude Opus 4.8  | `claude-opus-4-8`          | 1M      | 128k    | Legacy; previous Opus generation   |
| Claude Opus 4.7  | `claude-opus-4-7`          | 1M      | 128k    | Legacy                             |
| Claude Sonnet 4.6| `claude-sonnet-4-6`        | 1M      | 128k    | Legacy                             |
| Claude Sonnet 4.5| `claude-sonnet-4-5`        | 200k    | 64k     | Legacy                             |

> **Claude Mythos 5** (`claude-mythos-5`) is intentionally **excluded** — it is
> invitation-only (Project Glasswing, defensive-cybersecurity) with no self-serve access,
> so it does not belong in a general research tool's picker. It can be added to the config
> JSON by a user who has been granted access.

### 6.3 Config-driven model catalog

The catalog lives in an editable JSON file (ships as a default, overridable in Settings), so
new models require no rebuild:

Prices are USD per million tokens (MTok) and drive cost tracking (§3.6):

```json
{
  "defaultModel": "claude-opus-5",
  "tiers": [
    { "tier": "Frontier", "name": "Claude Fable 5",   "id": "claude-fable-5",   "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 10, "priceOutputMTok": 50, "vision": true },
    { "tier": "Deep",     "name": "Claude Opus 5",     "id": "claude-opus-5",    "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 5,  "priceOutputMTok": 25, "vision": true },
    { "tier": "Balanced", "name": "Claude Sonnet 5",   "id": "claude-sonnet-5",  "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 3,  "priceOutputMTok": 15, "vision": true },
    { "tier": "Fast",     "name": "Claude Haiku 4.5",  "id": "claude-haiku-4-5", "contextTokens": 200000,  "maxOutputTokens": 64000,  "priceInputMTok": 1,  "priceOutputMTok": 5,  "vision": true }
  ],
  "additional": [
    { "name": "Claude Opus 4.8",   "id": "claude-opus-4-8",   "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 5, "priceOutputMTok": 25, "vision": true },
    { "name": "Claude Opus 4.7",   "id": "claude-opus-4-7",   "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 5, "priceOutputMTok": 25, "vision": true },
    { "name": "Claude Sonnet 4.6", "id": "claude-sonnet-4-6", "contextTokens": 1000000, "maxOutputTokens": 128000, "priceInputMTok": 3, "priceOutputMTok": 15, "vision": true },
    { "name": "Claude Sonnet 4.5", "id": "claude-sonnet-4-5", "contextTokens": 200000,  "maxOutputTokens": 64000,  "priceInputMTok": 3, "priceOutputMTok": 15, "vision": true }
  ]
}
```

*Source: Anthropic "Models overview," platform.claude.com — retrieved 2026-08-03. Prices
shown are the standard synchronous rates as of that date (Sonnet 5 has introductory pricing
of $2/$10 through 2026-08-31; not encoded here). Because IDs, prices, and generations change,
the app treats this JSON as the source of truth and (future) can refresh model metadata via
the Models API (`/v1/models`).*

---

## 7. Architecture

### 7.1 Process & layers
- **WPF UI (C#, .NET 8)** — MVVM (CommunityToolkit.Mvvm). Modern styling via a control
  library (e.g. WPF-UI / Fluent). SkiaSharp or WebView2 for diagram/markdown preview.
- **Core domain (C# class library)** — projects/resources/artifacts services, versioning,
  context assembly, export.
- **Persistence** — EF Core + SQLite (or Microsoft.Data.Sqlite + Dapper); FTS5 for search.
- **AI gateway (C#)** — abstracts generation behind an `IChatService` / `IArtifactService`;
  talks to the Agent SDK sidecar.
- **Agent SDK sidecar (Node.js/TypeScript)** — see §7.2.

### 7.2 Why a sidecar (important)
The **Claude Agent SDK ships as TypeScript and Python packages — there is no native .NET
SDK.** To use it from WPF we run it as a **local sidecar process**:

- A small Node.js/TypeScript host bundles the Agent SDK and exposes a **local IPC surface**
  — stdio JSON-lines *or* a loopback WebSocket (`127.0.0.1`, ephemeral port, per-launch
  token). Recommended: **WebSocket** for clean streaming.
- WPF launches the sidecar on startup, passes the API key over a secure channel (never on
  the command line), and streams requests/responses.
- The sidecar is packaged with the app (bundled Node runtime or a compiled single-file
  binary via `pkg`/`node --experimental-sea`).

**Direct-API fallback (decided: build it).** In addition to the sidecar, the app ships a
**C# direct Anthropic Messages API** implementation of `IChatService` (no sidecar, no Node).
The Agent SDK sidecar is the **primary** path (agentic loop, built-in tool orchestration,
prompt-caching helpers); the direct-API path is the **fallback** for environments where the
sidecar can't run and for a leaner deployment. Both implement the same `IChatService` /
`IArtifactService` contracts, so the rest of the app is unaware which is active; selectable
in Settings. Cost/usage capture (§3.6) works identically on both since both surface API
usage fields.

### 7.3 Request flow (generation)
```
WPF ViewModel
  → IChatService.Ask(project, conversation, message, model, resourceScope)
    → assemble system prompt (custom instructions) + resources + history
    → send over IPC to sidecar
      → Agent SDK query() with selected model + streaming
    ← stream tokens back → UI; persist Message + usage on completion
```

### 7.4 Built-in tool set (fixed, not user-extensible)
The Agent SDK loop is given a **fixed, curated set of tools** — the same kind Claude Code
uses for file work — so the model can read, search, and write within the project's sandbox.
This is *not* a user-extensible tool/MCP marketplace; the set is closed, which keeps
"reduced capabilities" true while enabling real grounding and artifact authoring.

**File & search tools (scoped to the project sandbox only):**
- `Glob` — find files by pattern within the project resources/artifacts dirs.
- `Grep` — content search across resource text and artifact versions.
- `Read` — read a resource's extracted text or an artifact version (images returned as
  vision content blocks, per §3.2.1).
- `Edit` / `Write` — create or modify **artifacts** (writes land as new artifact versions via
  the artifact service, never silent overwrites — §3.4 versioning still applies).

**Artifact tools:**
- `emit_artifact` / `update_artifact` — structured artifact creation/update contract.

**Sandboxing (critical):** every tool is confined to the active project's directory tree
(`/projects/{projectId}/...`). Path traversal outside the sandbox is rejected. The model
cannot touch the user's wider filesystem, the SQLite DB, or other projects. All tool calls
are logged and visible in the conversation (transparency).

### 7.5 Security
- API key stored via **Windows Credential Manager / DPAPI**, never in SQLite or plaintext.
- Sidecar bound to loopback only, with a per-session auth token.
- No telemetry by default; all data local.

---

## 8. Non-functional requirements

- **Performance:** app cold start < 3s; streaming first-token latency bounded by API;
  resource extraction async with progress.
- **Reliability:** generation is cancellable; a crashed sidecar auto-restarts; partial
  streams are recoverable (persist on completion, mark interrupted otherwise).
- **Data safety:** SQLite WAL mode; periodic integrity check; export/backup of a project as
  a zip (db subset + files).
- **Accessibility:** keyboard-navigable, screen-reader labels on primary controls.
- **Rate limits (nod to prior product pain):** surface 429s clearly with retry/backoff in
  the sidecar; show usage/token counts. (Full durable job queue is out of v1 scope.)

---

## 9. Milestones

| Milestone | Scope |
|-----------|-------|
| **M0 — Skeleton** | WPF shell, MVVM, SQLite schema + migrations, settings + secure key storage, projects CRUD. |
| **M1 — Resources** | Add/extract text/file/URL resources; **image resources via vision + caption cache**; preview; enable/disable; token estimates; FTS. |
| **M2 — Q&A** | Sidecar + **built-in file tools (Glob/Grep/Read/Edit/Write), sandboxed**; conversations; model selector; streaming; per-turn metadata + actions; **image attachments in-thread**. |
| **M3 — Artifacts** | Promote/generate/blank artifacts; versioning; diff; manual + "Edit with Claude". |
| **M4 — Cost & deliverable** | **Cost tracking: per-turn / per-chat / consolidated per-project + CSV export**; export MD/DOCX/PDF/XLSX; project backup zip; polish. |
| **M5 — Scale/reliability** | 429 backoff, prompt caching, context-budget warnings, optional budget guardrails. |

---

## 10. Open questions / future

1. **RAG vs. full-context** — v1 uses full-context with a budget + manual scoping; add
   embeddings/RAG when projects outgrow the window.
2. **Direct-API mode** — ship the C# direct Anthropic path as a no-sidecar fallback.
   **Resolved: yes** — build it as the fallback behind `IChatService` (the sidecar remains
   the primary Agent SDK path).
3. **Image resources** — **Resolved:** supported from v1 via Claude's native **vision**
   (no OCR/vision library), with an optional caption cache for search/preview (§3.2.1).
   Scanned-PDF text extraction beyond native handling can still be revisited later.
4. **Cross-device sync / cloud backup** — **Resolved: deferred** (explicitly out of scope).
5. **Artifact type expansion** — **Resolved: in the roadmap** — HTML preview, richer
   spreadsheets, and slide decks are planned post-v1 additions.
