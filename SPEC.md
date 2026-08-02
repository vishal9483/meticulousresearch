# MeticulousResearch Desktop — Specification

> A polished, local-first Windows desktop application that gives a market-research analyst a
> **purpose-built workspace** for producing publication-quality industry research —
> market/competitive/customer/business intelligence reports, forecasts, and briefs — grounded
> in the analyst's own source material and drafted with Claude. It is **focused, not
> feature-thin**: every capability an analyst needs to go from raw sources to a branded,
> exportable deliverable is present and complete in **version 1.0**.

- **Status:** v1.0 Specification — release-ready scope (implementation to follow)
- **Product owner audience:** Meticulous Research (market intelligence & business consulting firm)
- **Date:** 2026-08-03
- **Platform:** Windows 11 (x64/arm64)
- **UI stack:** .NET 8 + WPF (C#), Fluent design system
- **AI backend:** Claude Agent SDK (primary, run as a sidecar — §7.2) + C# direct-API fallback
- **Storage:** Local-first — SQLite (metadata) + files on disk (blobs); per-project backup/restore

### Product vision
MeticulousResearch Desktop is the tool a Meticulous Research analyst opens to turn a folder of
interviews, filings, datasets, and web sources into a **finished research report** — with the
firm's structure, tone, and branding — in a fraction of the usual time. It is not a general
chat client; it is a **research production environment**. From version 1.0 it looks and feels
like a professional, shippable product: coherent visual identity, first-run onboarding,
report-grade export, dependable generation (retry/backoff, caching), and cost transparency.

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

### 1.2 Scope boundaries (focused by design)
MeticulousResearch is deliberately scoped to do **one job completely**: research production.
The following are intentional product boundaries — not gaps — chosen so the app stays fast,
private, and easy to trust. Each is a considered decision, and the in-scope alternative is
noted.

- **Single-user & local-first** — no account system, cloud sync, or multi-user collaboration.
  *Instead:* per-project backup/restore (zip) and a portable data directory make hand-off and
  archiving trivial (§8).
- **Project-scoped work** — no general (project-less) chat; every conversation and deliverable
  lives inside a research project so provenance is always intact.
- **Curated, not open-ended, tool use** — no user-extensible tool/computer-use/MCP
  marketplace. *Instead:* a complete, sandboxed built-in tool set (file read/search/edit/write
  + artifact emit) backs grounding and authoring (§7.4).
- **Authoring, not execution** — generated code/table artifacts are authored and exported, not
  run. No arbitrary code execution keeps the app safe on an analyst's machine.
- **Vision in, no media generation** — image *input* is fully supported (Claude reads charts,
  scans, and screenshots via native vision, §3.2). No image generation, voice, or browser
  automation.
- **Windows desktop** — a first-class native Windows 11 experience; no mobile/web build in v1.

None of these limit an analyst's ability to produce a complete, professional deliverable — the
entire source-to-report workflow is supported end-to-end in v1.0.

### 1.3 Design principles
1. **Professional from first launch** — the app looks and behaves like finished commercial
   software: coherent branding, guided first run, sensible defaults, no dead ends or
   placeholder screens.
2. **Local-first & private** — all data on the user's machine; the only network egress is
   to the Anthropic API via the Agent SDK.
3. **Project as the unit of work** — everything hangs off a research project.
4. **Provenance everywhere** — every artifact records the model, resources, and prompt that
   produced it; deliverables can cite their sources.
5. **Nothing is lost** — artifacts and conversations are versioned; edits never destroy
   prior state; projects are backup/restore-able.
6. **Deliverable-grade output** — the end product is a report a firm can ship: branded,
   structured, exportable to DOCX/PDF/XLSX with a cover page, TOC, and consistent styling.
7. **Dependable under real load** — generation survives rate limits (retry/backoff), reuses
   context via prompt caching, and always tells the user what a piece of work costs.

---

## 2. Personas & primary use cases

**Primary persona: the research analyst.** Works at a market-intelligence firm producing
syndicated and custom studies — market sizing and 10-year forecasts, competitive landscapes,
customer/buyer analysis, and executive briefings across industries (healthcare, semiconductors,
food & beverage, energy, automotive, etc.). Starts from a pile of source material (analyst
interviews, company filings, datasets, prior reports, web sources) and must produce a
polished, branded, client-ready deliverable.

**Secondary persona: the research manager / reviewer.** Reviews drafts, compares versions,
checks provenance and cost, and exports the final report for the client.

Primary flows:
1. Create a research project from a **template** (e.g. "Market Research Report",
   "Competitive Landscape") or blank; set custom instructions (house style, tone, forecast
   horizon).
2. Add resources (paste notes, drop in PDFs/DOCX/XLSX/CSV, add URLs, attach charts/images).
3. Ask questions in a conversation, picking a model per the task (fast lookups vs. deep
   synthesis), grounded in the project's resources.
4. Promote a good answer — or ask Claude to draft from a deliverable template — into an
   **artifact** (report section, forecast table, SWOT, competitive matrix).
5. Assemble sections into a full report; iterate via follow-ups; compare versions.
6. Export a **branded, publication-quality** report (DOCX/PDF with cover, TOC, headers) or
   dataset (XLSX); review cost/usage; back up the project.

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

### 3.4 Artifacts & research deliverables
- **Definition:** a substantial, standalone, editable output. Mirrors Claude Artifacts, tuned
  for research deliverables.
- **Types (v1):**
  - Markdown / rich text document
  - Plain text
  - Code snippet (syntax-highlighted; not executed)
  - Table / dataset (backed by CSV/rows; exports to XLSX)
  - Diagram (Mermaid source rendered to preview/SVG)
- **Creation paths:**
  1. *Promote* an assistant turn to a new artifact.
  2. *Generate* directly ("New artifact" → prompt + model + resources).
  3. *Generate from a **deliverable template*** (§3.4.1) — the recommended path for reports.
  4. *Create blank* and edit manually.

#### 3.4.1 Deliverable templates (research-grade, from v1)
To produce firm-quality output out of the box, the app ships a library of **research
deliverable templates**. A template is a structured prompt + section scaffold + output format
that steers Claude to produce a professionally organized artifact grounded in the project's
enabled resources. Templates are **config-driven** (editable JSON/Markdown scaffolds, same
philosophy as the model catalog §6.3) so the firm can add its own house formats without a
rebuild.

Bundled templates (v1):

| Template | Output shape | Typical sections |
|----------|--------------|------------------|
| **Market Research Report** (flagship) | Long-form document | Executive summary; market definition & scope; market sizing & 10-yr forecast; segmentation; drivers/restraints/opportunities; competitive landscape; regional analysis; recommendations; methodology & sources |
| **Executive Summary / Brief** | 1–2 page document | Key findings, implications, recommended actions |
| **Competitive Landscape** | Table + narrative | Company, positioning, offerings, strengths/weaknesses, share |
| **Market Forecast Model** | Table/dataset (→XLSX) | Segment × year matrix, CAGR, base/optimistic/conservative |
| **SWOT / Porter's Five Forces** | Structured document/diagram | Framework-driven analysis |
| **Company / Vendor Profile** | Document | Overview, financials, product lines, strategy, SWOT |
| **Customer / Buyer Insights** | Document | Personas, needs, buying criteria, voice-of-customer |
| **Trend / Technology Scan** | Document | Emerging tech, maturity, adoption, implications |

- Each template declares: id, display name, description, target artifact type, section
  scaffold, a generation prompt (with placeholders for scope/horizon/region), and a default
  model tier recommendation.
- Templates enforce **grounding-first** prompting: they instruct the model to cite which
  in-scope resources support each claim and to flag unsupported assertions, so deliverables
  are defensible.
- The "New artifact" and "New project" flows both surface the template gallery with previews.
- **Versioning** — every follow-up edit or regeneration creates a **new immutable version**;
  the artifact keeps an ordered version history. Each version records the model, prompt,
  in-scope resources, and timestamp.
- **Diff** — side-by-side / inline diff between any two versions.
- **Editing** — direct manual edit (creates a version) *or* "Edit with Claude" (follow-up
  instruction creates a version).
- **Management:** rename, set current version, revert to a version, duplicate, delete,
  promote-to-resource.
- **Compose a full report** — multiple artifacts can be ordered into a single **report
  compilation** (a document artifact that references sections in order) and exported as one
  branded file, so a report assembled from sections exports as a cohesive deliverable.

#### 3.4.2 Branded, publication-quality export (from v1)
Export is a first-class, deliverable-grade feature — not a raw content dump.

- **Formats:** Markdown, DOCX, PDF, and (for tables/forecast models) XLSX. Export uses the
  current version (or the composed report order).
- **Branded document theme:** DOCX/PDF exports apply a professional report template with:
  - a **cover page** (report title, subtitle, date, project/author, optional firm logo);
  - an auto-generated **table of contents** with page numbers;
  - **running headers/footers** (report title, page numbers, confidentiality notice);
  - consistent heading styles, tables, captions, and a **sources/methodology** section;
  - a configurable **color accent and logo** in Settings (defaults to a professional navy
    corporate palette, §3.7) so output matches the firm's identity.
- **Fidelity:** headings, tables, lists, code blocks, and Mermaid diagrams (rendered to
  images) all carry through to DOCX/PDF. XLSX export preserves typed columns and formulas
  where present.
- **Deterministic & offline:** export runs locally (no network) and produces the same output
  every time; a preview is shown before saving.
- **Export presets:** "Client-ready report" (full branding + cover + TOC), "Internal draft"
  (minimal chrome), and "Plain" (content only).

### 3.5 Global / cross-cutting
- **Settings** — API key entry (stored via Windows DPAPI/credential vault, not plaintext),
  API base URL / endpoint (defaults to the public Anthropic API, overridable for gateway/
  proxy deployments), default model, context budget, data directory location, theme
  (light/dark/system), telemetry off by default.
- **API key & endpoint resolution** — both the key and the base URL may be supplied via the
  environment, which **takes precedence** over stored settings (§7.5):
  - **API key:** `ANTHROPIC_API_KEY` env var → secure key store (Credential Manager/DPAPI) →
    "no key configured" error.
  - **Base URL:** `ANTHROPIC_BASE_URL` env var → persisted base-URL setting → default public
    Anthropic API. The endpoint is **never hardcoded**; the direct-API client and the sidecar
    both honor the resolved value so gateway/proxy deployments work without a rebuild.
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

### 3.7 Visual identity & design system
The app ships with a coherent, professional design language so it reads as finished commercial
software from v1.0.

- **Brand palette:** a corporate **navy/blue** primary (evoking a market-intelligence firm's
  trusted-advisor tone) with a single accent, neutral grays for surfaces, and semantic
  success/warning/error colors. Full **light and dark** themes plus "follow system."
- **Typography:** a clean, professional type scale (a modern sans for UI; a readable serif or
  sans option for exported report body text). Consistent spacing and 8px grid.
- **Component library:** built on a Fluent WPF control set (§7.1) — buttons, inputs, tables,
  cards, dialogs, toasts, and a command palette all drawn from one styled kit; no unstyled
  default WPF chrome.
- **Iconography:** a single consistent icon set (Fluent/Lucide-style).
- **Motion:** subtle, purposeful transitions (streaming cursor, panel slide, progress); never
  gratuitous.
- **Empty & loading states:** every list/view has a designed empty state with a clear
  call-to-action (e.g. "No projects yet — create your first research project") and skeleton
  loaders during async work. No blank screens.
- **Error states:** human-readable, actionable messages (missing API key, offline, rate
  limited, extraction failed) with a recovery action — never a raw stack trace.
- **App identity:** application icon, product name, and About screen with version; installer
  branding (§8).

### 3.8 First-run onboarding
On first launch the app guides the user to a working state so it's usable immediately:

1. **Welcome** — brief product intro and privacy statement (local-first; where data lives).
2. **API key setup** — enter the Anthropic API key; stored securely (§7.5); a "Test key"
   button verifies connectivity and lists available models.
3. **Defaults** — pick default model tier, theme, and data directory (sensible defaults
   pre-filled).
4. **Sample project (optional)** — offer to create a fully populated **sample research
   project** (a couple of resources + an example Market Research Report artifact) so the user
   can see the whole workflow before adding their own material.
5. **Done** — land on the Projects home with contextual hints on the primary actions.

Onboarding is skippable and re-runnable from Settings.

---

## 4. UX / screen inventory

0. **First-run onboarding** — welcome, API key + test, defaults, optional sample project (§3.8).
1. **Home / Projects list** — grid or list of project cards; create/search; archived toggle;
   "New project" opens the **template gallery** (§3.4.1). Designed empty state for first use.
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
   history rail, diff mode, **branded export menu with preview** (§3.4.2), "Edit with Claude"
   prompt bar, and template/section scaffold when created from a deliverable template.
5b. **Template gallery** — browse research deliverable templates (§3.4.1) with previews and
   descriptions; launch a new project or artifact from one.
5c. **Report composition view** — order artifacts into a single report and export as one
   branded document (§3.4.1 / §3.4.2).
6. **Project dashboard** — resource/artifact/conversation counts, last activity, and the
   **consolidated cost panel** (§3.6): total spend with breakdowns by model, by
   conversations-vs-artifacts, and by time window; CSV export.
7. **Settings** — API key (+ test), defaults, storage, appearance/theme, **brand: logo &
   accent color for exports**, model catalog & pricing, deliverable-template management,
   optional per-project budget, re-run onboarding, About/version.

Visual language: clean, professional, corporate navy palette (§3.7); light/dark themes; WPF
with a modern Fluent control library (see §7.1). Every screen has designed empty, loading, and
error states.

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
This is *not* a user-extensible tool/MCP marketplace; the set is closed and curated, which
keeps the app safe and focused while enabling complete grounding and artifact authoring.

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
- **Key resolution order:** `ANTHROPIC_API_KEY` environment variable (if set) **wins**, then
  the secure key store, then a "no key configured" error. The env var is read at request/
  launch time and, like the stored key, is never written to SQLite, settings files, or the
  sidecar command line.
- **Endpoint resolution order:** `ANTHROPIC_BASE_URL` environment variable (if set) **wins**,
  then the persisted base-URL setting, then the default public Anthropic API. The resolved
  base URL is passed to both the direct-API client and the sidecar; it is never hardcoded.
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
- **Accessibility:** keyboard-navigable, screen-reader labels on primary controls, WCAG-AA
  contrast in both themes.
- **Reliability under rate limits (v1 core):** the AI gateway handles HTTP 429 and transient
  5xx with **automatic exponential backoff + jitter and honoring `retry-after`**, surfacing a
  clear "retrying…" state with attempt count rather than failing the generation. Streaming
  that is interrupted is resumable/persisted (§8 reliability). This directly addresses the
  prior product pain of analysts being rate-limited mid-document. (A *durable multi-machine
  job queue* remains a future scale item — see §10 — but single-machine resilience is in v1.)
- **Prompt caching (v1 core):** system prompt (custom instructions) and stable resource
  context are sent with cache breakpoints so repeated turns/regenerations reuse cached input,
  cutting latency and cost; cache-read/write tokens are metered (§3.6).
- **Context-budget management (v1 core):** before send, the app estimates token usage against
  the model's context window and the configured budget, warns when exceeded, and helps the
  user deselect resources (§3.2). No silent truncation.
- **Installer & updates:** signed Windows installer (MSIX or WiX/MSI); app version shown in
  About; update mechanism (at minimum, in-app "update available" notice).

---

## 9. Milestones & release plan

**All of M0–M6 constitute v1.0.** The app is not considered shippable until the full
source-to-branded-deliverable workflow, professional visual identity, and reliability features
are complete. Milestones are a build order, not a scope-reduction; nothing an analyst needs to
produce a client-ready report is deferred past v1.0.

| Milestone | Scope |
|-----------|-------|
| **M0 — Skeleton** | WPF shell + **design system/theming (§3.7)**, MVVM, SQLite schema + migrations, settings + secure key storage, projects CRUD. |
| **M1 — Resources** | Add/extract text/file/URL resources; **image resources via vision + caption cache**; preview; enable/disable; token estimates; FTS; context-budget estimation & warnings. |
| **M2 — Q&A** | Sidecar + **direct-API fallback**; **built-in file tools (Glob/Grep/Read/Edit/Write), sandboxed**; conversations; model selector; streaming; per-turn metadata + actions; **image attachments in-thread**; **429 backoff + prompt caching**. |
| **M3 — Artifacts & templates** | Promote/generate/blank artifacts; **deliverable templates (§3.4.1)**; versioning; diff; manual + "Edit with Claude"; report composition. |
| **M4 — Deliverable & cost** | **Branded publication-quality export MD/DOCX/PDF/XLSX (§3.4.2)**; **cost tracking per-turn / per-chat / consolidated per-project + CSV export**; project backup/restore zip. |
| **M5 — Onboarding & polish** | **First-run onboarding + sample project (§3.8)**; empty/loading/error states; accessibility pass; command palette & shortcuts; About screen. |
| **M6 — Package & release** | Signed installer (MSIX/MSI), app icon/branding, update notice, docs; **v1.0 acceptance (§9.1)**. |

### 9.1 v1.0 acceptance criteria (the quality bar)
v1.0 ships only when a new user can, on a clean Windows 11 machine:

1. Install via a signed installer and launch to a branded first-run onboarding.
2. Enter and validate an API key; create a research project from a deliverable template.
3. Add mixed resources (PDF, DOCX, XLSX, URL, image) and see them extracted, previewed, and
   token-estimated.
4. Hold a grounded, streaming conversation with model selection and per-turn cost.
5. Generate a **Market Research Report** artifact from a template, iterate with "Edit with
   Claude," and compare versions.
6. Export a **branded, client-ready PDF/DOCX** (cover, TOC, headers) and an XLSX forecast.
7. See consolidated project cost and export usage CSV.
8. Experience a rate-limit event and observe automatic retry/backoff without losing work.
9. Back up and restore a project.
10. Do all of the above with no crashes, no unstyled/placeholder screens, and no raw errors.

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
4. **Cross-device sync / cloud backup** — **Resolved: deferred** (out of scope; per-project
   backup/restore zip covers hand-off in v1).
5. **Artifact type expansion** — **Resolved: in the roadmap** — HTML preview, richer
   spreadsheets, and slide decks are planned post-v1 additions.
6. **Durable multi-machine job queue** — post-v1 scale item. v1 delivers single-machine
   resilience (429 backoff, resumable/persisted streams, prompt caching, §8); a
   Redis/BullMQ-style durable queue is only warranted if the app grows to multi-machine or
   unattended batch generation.
7. **Firm template pack** — post-v1, ship a Meticulous-Research-specific pack of deliverable
   templates and an export theme matching the firm's exact house style (logo, fonts, section
   conventions), building on the config-driven template/branding system (§3.4.1, §3.7).
