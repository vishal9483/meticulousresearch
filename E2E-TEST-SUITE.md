# MeticulousResearch Desktop — End-to-End Test Suite

> **Status:** Draft test plan (scenarios only — not yet implemented).
> **Purpose:** Prove the full **source → grounded conversation → artifact → branded deliverable**
> workflow works as an integrated product, on top of the per-feature `@unit`/`@ui` tests that
> already live under [`docs/features/`](docs/features/). This suite is journey-oriented: each item
> is a complete user story that crosses feature boundaries, mapped back to the v1.0 acceptance bar
> in [`SPEC.md` §9.1](SPEC.md).

---

## 1. What "end-to-end" means here

The existing test pyramid (see [`docs/TESTING-STRATEGY.md`](docs/TESTING-STRATEGY.md)) is:

- `@unit` — Core services & view-models, driven by xUnit, no window. (Business rules live here.)
- `@ui` — FlaUI drives the real WPF window for a single feature (wiring proof).
- `@manual` — human-verified visual/subjective checks.

This document adds a **fourth layer on top**: `@e2e` **journeys** — long, multi-feature flows that
exercise the composed app the way an analyst actually uses it, end to end. A journey deliberately
touches many features (projects + resources + conversations + artifacts + export + cost) in one run
so we catch **integration seams** the per-feature gate misses — exactly the class of bug recorded in
`/memories/repo/build-progress.md` (broken DI composition root passed the headless gate green).

### Tag conventions for this suite

| Tag | Meaning | Driver |
|-----|---------|--------|
| `@e2e` | Full multi-feature user journey | FlaUI (UIA3) against the built WPF app |
| `@e2e @unit` | Same journey expressed at the service/VM layer (fast, headless) | xUnit orchestrating real Core services + `FakeChatService` |
| `@requires-key` `@requires-network` | Only the live-API acceptance journey (J-13) hits the real Anthropic API | live |
| `@manual` | Visual/subjective sign-off inside a journey | human + screenshot |

**Strategy:** every journey below is authored **twice** where practical — once as `@e2e @unit`
(orchestrating the real Core + view-models with a scripted `FakeChatService`, runnable in the CI
gate) and once as `@e2e` (FlaUI, driving the actual window, `Category=ui`, compile-in-gate /
run-nightly). The `@e2e @unit` variant is the merge gate; the FlaUI variant is the release gate.

---

## 2. Test environment, doubles & determinism

All journeys run against a **hermetic, disposable environment** — no test touches real
`%LOCALAPPDATA%` data or the network (except J-13).

- **Data dir** — a fresh temp directory per journey; `db.sqlite` + `/projects/...` created from
  clean, torn down after. WAL mode as in production.
- **Clock** — injected `IClock` (`FakeClock`) so timestamps, backoff jitter, and cost time-windows
  (today / this week / all-time) are deterministic.
- **AI backend** — `FakeChatService : IChatService` replays scripted token streams, `ChatUsage`
  numbers (input/output/cache-read/cache-write tokens), tool-call logs, and error codes (429, 5xx,
  auth) so streaming, backoff, caching, and cost are reproducible. The env-first credential
  resolution (`ANTHROPIC_API_KEY` / `ANTHROPIC_BASE_URL`) is exercised via `FakeEnvironment`.
- **Model catalog & prices** — pinned to a test copy of `default-model-catalog.json` so cost math
  and tier labels are stable regardless of catalog updates.
- **Files** — sample fixtures (see §3) are copied into the temp dir at journey start.
- **Determinism rule** — token estimates, cost, diff output, and export bytes must be identical
  across runs for identical inputs (SPEC §3.4.2 "deterministic & offline").

### 2.1 Shared fixtures (`tests/e2e/fixtures/`)

| Fixture | Used by | Notes |
|---------|---------|-------|
| `interview.txt` | resources | plain text paste / TXT |
| `filing.pdf` | resources | multi-page PDF extraction |
| `brief.docx` | resources | DOCX extraction |
| `market-data.xlsx` | resources | tabular extraction |
| `segments.csv` | resources | CSV → table |
| `chart.png`, `scan.jpg` | image vision | vision caption + attachment |
| `https://example.test/report` | url resource | served by a loopback stub, not real network |
| `logo.png` | branding/export | firm logo on cover page |
| `scripts/*.json` | `FakeChatService` | scripted streams: normal, 429-then-success, 5xx, tool-use, artifact-emit |

---

## 3. Personas used in journeys

- **Ana** — research analyst, primary persona. Runs the full source-to-report flow.
- **Ravi** — research manager/reviewer. Reviews versions, checks provenance & cost, exports final.

---

## 4. End-to-end journeys

Each journey has a stable **ID (J-nn)**, the SPEC §9.1 acceptance criteria it covers, and Gherkin.
Steps are written from the user's perspective; the driver (FlaUI vs. service/VM) is chosen by tag.

---

### J-00 — Cold start integrates the whole app (smoke)

> Guards the exact gap that bit M0: a broken DI composition root that the headless gate missed.

```gherkin
@e2e @unit
Scenario: The application composes and resolves its whole object graph
  Given a clean data directory and no API key configured
  When the application host is built and started
  Then the DI container resolves the ShellViewModel and MainWindow without error
  And every navigation section (Projects, Settings, About) can be constructed
  And no service registration is missing

@e2e
Scenario: The app cold-starts to a branded, non-blank first screen
  Given a clean machine profile with no prior data
  When I launch MeticulousResearch
  Then the main window appears within 3 seconds
  And it shows the branded product identity (name, icon, navy theme)
  And no screen is blank, unstyled, or shows a raw error
```

---

### J-01 — First-run onboarding to a working state
**Covers §9.1: 1, 2** (launch to branded onboarding; enter & validate key).

```gherkin
@e2e
Scenario: A brand-new user is guided from welcome to the Projects home
  Given a first launch with no API key and no projects
  When the onboarding wizard opens on the Welcome step
  And I read the local-first privacy statement and continue
  And I enter a valid API key and click "Test key"
  Then the app confirms connectivity and lists available models
  When I pick a default model tier, theme, and data directory and continue
  And I choose to create the optional sample project
  Then onboarding completes and I land on the Projects home
  And a fully populated sample project is present with resources and an example report artifact
  And the primary actions show contextual first-run hints

@e2e @unit
Scenario Outline: Key validation gates progression and never persists an env key
  Given onboarding is on the API-key step
  And the environment variable "<envKey>" is "<envValue>"
  When I enter "<typedKey>" and test the key
  Then the tested key is "<effectiveKey>"
  And an env-provided key is never written to settings or SQLite

  Examples:
    | envKey             | envValue     | typedKey    | effectiveKey |
    | ANTHROPIC_API_KEY  | env-secret   | typed-key   | env-secret   |
    | ANTHROPIC_API_KEY  |              | typed-key   | typed-key    |

@e2e
Scenario: Onboarding is skippable and re-runnable from Settings
  Given I skipped onboarding on first launch
  Then I still land on a usable Projects home
  When I later open Settings and choose "Re-run onboarding"
  Then the onboarding wizard opens again from the Welcome step
```

---

### J-02 — Create a research project from a deliverable template
**Covers §9.1: 2** (create a project from a template).

```gherkin
@e2e
Scenario: Ana starts a project from the Market Research Report template
  Given the Projects home with an empty-state call-to-action
  When I click "New project" and the template gallery opens
  Then I see research deliverable templates with previews and descriptions
  When I choose "Market Research Report" and set name, custom instructions, and default model
  Then a new project is created with those custom instructions and default model
  And I land in the three-pane project workspace
  And the project dashboard shows zero resources, zero artifacts, zero cost
```

---

### J-03 — Add mixed resources and see them extracted, previewed, token-estimated
**Covers §9.1: 3** (PDF, DOCX, XLSX, URL, image extracted/previewed/estimated).

```gherkin
@e2e
Scenario: Ana loads the project with heterogeneous source material
  Given an open project with no resources
  When I paste interview notes as a text resource
  And I upload "filing.pdf", "brief.docx", and "market-data.xlsx"
  And I add the URL "https://example.test/report"
  And I add the image "chart.png"
  Then each resource appears in the resources table with type, byte size, and a token estimate
  And each non-image resource shows previewable extracted text
  And the image resource shows a thumbnail and a cached vision caption
  And the URL resource retains its original URL and shows converted text
  And the dashboard resource count reflects all added resources

@e2e @unit
Scenario Outline: Extraction pipeline stores both original blob and extracted text
  When I add a "<type>" resource from "<fixture>"
  Then the original blob is stored under the project's files dir
  And extracted text is stored and searchable
  And a deterministic token estimate is recorded

  Examples:
    | type  | fixture          |
    | text  | interview.txt    |
    | pdf   | filing.pdf       |
    | docx  | brief.docx       |
    | xlsx  | market-data.xlsx |
    | csv   | segments.csv     |
    | url   | report (stub)    |
    | image | chart.png        |
```

---

### J-04 — Manage resource scope and stay within the context budget
**Covers §3.2, §8** (selection model, budget warnings, no silent truncation).

```gherkin
@e2e
Scenario: Toggling resources changes the estimated context budget
  Given a project whose enabled resources fit within the context budget
  When I open the resource-scope panel
  And I enable a large resource that pushes the estimate over the budget
  Then the app shows a context-budget warning with the over-budget amount
  And it offers to deselect resources rather than truncating silently
  When I disable that resource
  Then the estimate returns under budget and the warning clears

@e2e @unit
Scenario: Only enabled resources contribute to the pre-send estimate
  Given three resources, two enabled and one disabled
  When the context budget is estimated for the next turn
  Then only the two enabled resources contribute tokens
  And the estimate is labeled "estimated" (local estimation, not billed usage)
```

---

### J-05 — Hold a grounded, streaming conversation with model selection and per-turn cost
**Covers §9.1: 4** (grounded, streaming conversation; model selection; per-turn cost).

```gherkin
@e2e
Scenario: Ana asks a grounded question and watches the answer stream
  Given a project with enabled resources
  When I start a new conversation and select the "Balanced" model tier
  And I ask "Summarize the competitive landscape from my sources"
  Then the assistant response streams token-by-token into the thread
  And the request was grounded in the project's custom instructions and enabled resources
  When the turn completes
  Then the turn shows the model used, input/output tokens, latency, and a computed cost badge
  And the conversation header shows a running total token count and cost
  And the in-scope resources are recorded on the turn

@e2e
Scenario: Stopping a generation persists a clean interrupted turn
  Given an assistant turn is streaming
  When I press Stop (Esc)
  Then streaming halts promptly
  And the partial turn is persisted and marked interrupted (no data loss)

@e2e @unit
Scenario Outline: The model is selectable per conversation and overridable per message
  Given a conversation whose default tier is "<convTier>"
  When I send a message overriding the tier to "<msgTier>"
  Then the assistant turn records model "<recordedModel>"

  Examples:
    | convTier | msgTier  | recordedModel     |
    | Balanced | Deep     | claude-opus-5     |
    | Fast     | Frontier | claude-fable-5    |
    | Deep     |          | claude-opus-5     |
```

---

### J-06 — Turn actions: copy, retry, edit-and-resend, promote to artifact
**Covers §3.3** (per-turn actions) and bridges into artifacts.

```gherkin
@e2e
Scenario: Ana curates a conversation using per-turn actions
  Given a completed assistant turn
  When I copy the turn
  Then its content is on the clipboard
  When I retry the turn with a different model
  Then a new turn is produced and records the new model and its own cost
  When I edit my earlier message and resend
  Then a new assistant turn replaces the downstream history consistently
  When I promote a strong turn to an artifact
  Then a new artifact is created carrying the turn's provenance (model, prompt, in-scope resources)
```

---

### J-07 — Image attachment in-thread (vision)
**Covers §3.2.1** (multimodal message, not just a persistent resource).

```gherkin
@e2e
Scenario: Ana attaches a chart directly to a message and asks about it
  Given a conversation using a vision-capable model
  When I attach "scan.jpg" to a new message and ask "What trend does this chart show?"
  Then the image renders as an inline thumbnail in the composer and the sent turn
  And the model receives the image as a vision content block
  And image tokens count toward the turn's usage and cost

@e2e @unit
Scenario: Selecting a non-vision model warns before sending an image
  Given a message with an image attachment
  When I select a model that does not accept image input
  Then the app warns and offers to switch to a vision-capable model
```

---

### J-08 — Rate-limit backoff without losing work
**Covers §9.1: 8, §8** (429 → automatic retry/backoff; work preserved).

```gherkin
@e2e
Scenario: A 429 mid-generation triggers visible retry, then succeeds
  Given the AI backend is scripted to return 429 with a retry-after, then succeed
  When I send a message
  Then the app shows a "retrying…" state with the attempt count
  And it honors retry-after with exponential backoff and jitter
  When the retry succeeds
  Then the final answer streams in and the turn persists normally
  And no user input or partial work was lost

@e2e @unit
Scenario Outline: The gateway retries transient failures and surfaces terminal ones
  Given the backend returns "<sequence>"
  When a generation runs
  Then the outcome is "<outcome>" after "<attempts>" attempt(s)

  Examples:
    | sequence            | outcome  | attempts |
    | 429,429,200         | success  | 3        |
    | 503,200             | success  | 2        |
    | 401                 | auth-error (no retry) | 1 |
```

---

### J-09 — Prompt caching reduces repeat cost
**Covers §8, §3.6** (cache breakpoints; cache tokens metered).

```gherkin
@e2e @unit
Scenario: A follow-up turn reuses cached system prompt and resource context
  Given a first turn that establishes cache breakpoints on instructions + stable resources
  When I ask a follow-up in the same conversation
  Then the second turn reports cache-read tokens
  And its cost reflects cache-read/cache-write rates from the model catalog
  And the conversation running cost includes the cache token effects
```

---

### J-10 — Generate a Market Research Report artifact from a template, iterate, and diff
**Covers §9.1: 5** (generate from template; iterate with Edit-with-Claude; compare versions).

```gherkin
@e2e
Scenario: Ana produces and refines a template-driven report artifact
  Given a project with enabled resources
  When I choose "New artifact" → "Market Research Report" template
  And I set scope, forecast horizon, and region, then generate
  Then an artifact is created with the template's section scaffold, grounded in enabled resources
  And version 1 records model, prompt, in-scope resources, and usage
  When I use "Edit with Claude" with the instruction "tighten the executive summary"
  Then version 2 is created immutably and version 1 is preserved
  When I open diff mode and compare v1 and v2
  Then the changes are shown side-by-side / inline
  When I revert to version 1
  Then the current version becomes v1 without destroying v2

@e2e @unit
Scenario: Manual edits and Claude edits both create immutable versions
  Given an artifact at version 1
  When I make a manual edit
  Then version 2 is created with created_by = user and zero generation usage
  When I run an "Edit with Claude" follow-up
  Then version 3 is created with created_by = claude and recorded usage
```

---

### J-11 — Compose a full report and export a branded, client-ready deliverable
**Covers §9.1: 6** (compose sections; export branded PDF/DOCX + XLSX forecast).

```gherkin
@e2e
Scenario: Ravi assembles sections into one branded report and exports it
  Given a project with several document artifacts and one forecast-table artifact
  When I open the report composition view and order the document artifacts into sections
  Then a single composed report references those sections in order
  When I set the firm logo and accent color in Settings
  And I export the composed report as "Client-ready report" to PDF
  Then a preview is shown before saving
  And the exported PDF has a cover page, an auto-generated TOC with page numbers, and running headers/footers
  And headings, tables, lists, code blocks, and Mermaid diagrams carry through
  When I export the same report to DOCX
  Then the DOCX applies the branded theme with the same structure
  When I export the forecast-table artifact to XLSX
  Then typed columns (and any formulas) are preserved

@e2e @unit
Scenario Outline: Export presets control document chrome deterministically
  Given a composed report and preset "<preset>"
  When I render the export to "<format>"
  Then cover/TOC/headers are "<chrome>"
  And exporting the same input twice produces byte-identical output

  Examples:
    | preset            | format | chrome    |
    | Client-ready report | PDF  | full      |
    | Internal draft    | DOCX   | minimal   |
    | Plain             | MD     | none      |
```

---

### J-12 — Consolidated cost & usage CSV export
**Covers §9.1: 7, §3.6** (consolidated project cost; export usage CSV).

```gherkin
@e2e
Scenario: The dashboard consolidates cost across conversations and artifacts
  Given a project with multiple conversations and generated artifact versions across model tiers
  When I open the project dashboard cost panel
  Then it shows total spend with a breakdown by conversations-vs-artifacts
  And a breakdown by model tier
  And a breakdown by time window (today / this week / all-time)
  When I export usage as CSV
  Then the CSV contains one row per billed turn/version with tokens and computed cost

@e2e @unit
Scenario: Cost is recomputed from stored tokens when catalog prices change
  Given historical turns storing input/output/cache tokens as ground truth
  When the model catalog price for a tier is updated
  Then the consolidated cost reprices historical usage consistently from the new prices
```

---

### J-13 — Live-API acceptance (single, gated, opt-in)
**Covers §9.1 end-to-end against the real Anthropic API.** Runs only with a real key; excluded from
the normal gate.

```gherkin
@e2e @requires-key @requires-network @manual
Scenario: The complete source-to-branded-deliverable flow works against the live API
  Given a real ANTHROPIC_API_KEY is configured
  And a project created from the Market Research Report template with real resources
  When I hold a grounded streaming conversation and generate a report artifact
  And I iterate with Edit-with-Claude and export a branded PDF and an XLSX forecast
  Then per-turn and consolidated cost reflect authoritative API usage fields
  And the whole flow completes with no crashes, no placeholder screens, and no raw errors
```

---

### J-14 — Backup and restore a project
**Covers §9.1: 9, §8** (backup/restore zip; hand-off).

```gherkin
@e2e
Scenario: A project round-trips through backup and restore intact
  Given a populated project (resources, conversations, artifacts with versions, cost history)
  When I create a backup zip of the project
  Then the backup contains the DB subset plus the project's files
  When I delete the project and restore from the backup
  Then the restored project has identical resources, conversation turns, artifact versions, and cost
  And FTS search over the restored content works

@e2e @unit
Scenario: A tampered or incomplete backup is rejected with a clear error
  Given a backup archive missing its manifest
  When I attempt to restore it
  Then restore fails with a human-readable, actionable error
  And no partial project is left behind
```

---

### J-15 — Full-text search across a project
**Covers §3.1** (FTS across resources, conversations, artifacts).

```gherkin
@e2e
Scenario: Ana finds a claim across all content types
  Given a project with resources, conversation turns, and artifact versions containing "market sizing"
  When I search the project for "market sizing"
  Then results include hits from resources, conversations, and artifacts
  And each hit shows a type, title, and matching snippet
  When I open a hit
  Then I navigate to that item in context
```

---

### J-16 — Built-in tool sandbox transparency & confinement
**Covers §7.4** (curated tools; sandboxed to the project; tool calls visible).

```gherkin
@e2e
Scenario: Tool calls during generation are visible in the conversation
  Given a generation scripted to use Glob, Grep, Read, and Write tools
  When the turn runs
  Then each tool call is logged and shown in the conversation for transparency
  And a Write tool call lands as a new artifact version (never a silent overwrite)

@e2e @unit
Scenario Outline: Tools cannot escape the active project's sandbox
  Given a tool call targeting "<path>"
  When the tool executes
  Then the outcome is "<result>"

  Examples:
    | path                                   | result   |
    | projects/{id}/resources/r1/extracted.txt | allowed |
    | ../other-project/db.sqlite             | rejected |
    | ../../Windows/System32/hosts           | rejected |
    | db.sqlite                              | rejected |
```

---

### J-17 — Command palette & keyboard shortcuts drive navigation
**Covers §3.5** (Ctrl+K palette; shortcuts).

```gherkin
@e2e
Scenario: Ana navigates the whole app from the keyboard
  Given any screen in the app
  When I press Ctrl+K
  Then the command palette opens
  When I search and select "New conversation"
  Then a new conversation opens in the current project
  And Ctrl+Enter sends a message, Esc stops a generation, and Ctrl+K jumps to search
```

---

### J-18 — Theming: light / dark / follow-system
**Covers §3.7** (full light & dark; follow system).

```gherkin
@e2e
Scenario: Switching theme restyles the entire app with no unstyled chrome
  Given the app in light theme
  When I switch to dark theme in Settings
  Then all screens, dialogs, and toasts adopt the dark palette
  And no default/unstyled WPF chrome appears
  When I switch to "Follow system" and the OS theme changes
  Then the app follows the system theme

@e2e @manual
Scenario: The visual identity reads as finished commercial software
  Given the app in both themes
  Then branding, typography, spacing, iconography, and motion match the design system checklist
```

---

### J-19 — Empty, loading, and error states everywhere
**Covers §3.7, §9.1: 10** (designed empty/loading/error; no dead ends or raw errors).

```gherkin
@e2e
Scenario Outline: Every primary list shows a designed empty state with a call-to-action
  Given a "<view>" with no items
  Then it shows a designed empty state with a clear primary action

  Examples:
    | view          |
    | Projects home |
    | Resources     |
    | Conversations |
    | Artifacts     |

@e2e
Scenario: Async work shows skeleton loaders, never a blank screen
  Given a slow resource extraction or generation
  Then a skeleton/progress state is shown while work is in flight

@e2e @unit
Scenario Outline: Failures surface human-readable, recoverable errors — never a stack trace
  Given the condition "<failure>"
  When I trigger the affected action
  Then I see a human-readable message with a recovery action

  Examples:
    | failure            |
    | missing API key    |
    | offline / no network |
    | rate limited       |
    | extraction failed  |
```

---

### J-20 — Offline behavior & credential/endpoint resolution
**Covers §3.5, §7.5** (offline browse/edit; env-first key & base URL).

```gherkin
@e2e
Scenario: Existing data is fully usable offline; generation fails clearly
  Given a populated project and no network
  Then I can browse and edit projects, resources, artifacts, and past conversations
  When I attempt a new generation
  Then I get a clear offline error state with a recovery action (no crash)

@e2e @unit
Scenario Outline: Key and base URL resolve environment-first and never persist the env value
  Given env "<envVar>" = "<envVal>" and stored setting "<stored>"
  When the effective "<field>" is resolved
  Then it equals "<effective>"
  And no environment value is written to settings, SQLite, or the sidecar command line

  Examples:
    | field    | envVar              | envVal              | stored          | effective           |
    | api-key  | ANTHROPIC_API_KEY   | env-key             | stored-key      | env-key             |
    | api-key  | ANTHROPIC_API_KEY   |                     | stored-key      | stored-key          |
    | base-url | ANTHROPIC_BASE_URL  | https://gw.test     | https://s.test  | https://gw.test     |
    | base-url | ANTHROPIC_BASE_URL  |                     | https://s.test  | https://s.test      |
    | base-url | ANTHROPIC_BASE_URL  |                     |                 | (public default)    |
```

---

### J-21 — Backend parity: sidecar vs. direct-API fallback
**Covers §7.2** (both implement `IChatService`; app unaware which is active).

```gherkin
@e2e @unit
Scenario Outline: Streaming, usage, and cost are identical across backends
  Given the "<backend>" backend selected in Settings
  When I run the same grounded conversation script
  Then the streamed content, recorded usage, and computed cost match the reference
  And per-turn provenance is captured identically

  Examples:
    | backend    |
    | sidecar    |
    | direct-api |

@e2e
Scenario: A crashed sidecar auto-restarts without losing the app
  Given the sidecar backend is active
  When the sidecar process crashes
  Then it auto-restarts
  And the app surfaces the interruption gracefully and remains usable
```

---

### J-22 — Accessibility pass
**Covers §8** (keyboard-navigable; screen-reader labels; WCAG-AA contrast).

```gherkin
@e2e
Scenario: The primary flow is fully keyboard-navigable with accessible names
  Given the project workspace
  Then every primary control is reachable by keyboard and exposes an accessible name to UIA
  And focus order is logical across the three panes

@e2e @manual
Scenario: Both themes meet WCAG-AA contrast
  Given the app in light and dark themes
  Then text and essential UI meet WCAG-AA contrast per the accessibility checklist
```

---

### J-23 — About screen & update notice
**Covers §3.7, §8** (About/version; in-app update-available notice).

```gherkin
@e2e
Scenario: The About screen shows product identity and version
  When I open the About screen
  Then it shows the product name, icon, and the current app version

@e2e @unit
Scenario Outline: The update service reports availability correctly
  Given the current version is "<current>" and the latest available is "<latest>"
  When an update check runs
  Then an update notice is "<shown>"

  Examples:
    | current | latest | shown      |
    | 1.0.0   | 1.1.0  | shown      |
    | 1.0.0   | 1.0.0  | not shown  |
```

---

### J-24 — Provenance & "nothing is lost" invariants (cross-cutting)
**Covers §1.3 principles 4 & 5** (provenance everywhere; nothing destroyed).

```gherkin
@e2e @unit
Scenario: Every generated output records its provenance
  Given a conversation turn and an artifact version generated from it
  Then each records the model, in-scope resources, prompt, and timestamp
  And promoting an artifact to a resource preserves that provenance chain

@e2e @unit
Scenario: Edits and regenerations never destroy prior state
  Given an artifact with several versions
  When I edit, regenerate, and revert
  Then all prior versions remain retrievable in order
  And no prior version's content is mutated
```

---

## 5. Traceability — journeys × SPEC §9.1 acceptance criteria

| §9.1 criterion | Journeys |
|----------------|----------|
| 1. Install → branded first-run onboarding | J-00, J-01 (installer itself: `installer` feature) |
| 2. Enter/validate key; project from template | J-01, J-02 |
| 3. Mixed resources extracted/previewed/estimated | J-03, J-04 |
| 4. Grounded, streaming conversation + model + per-turn cost | J-05, J-06, J-07 |
| 5. Generate report from template; iterate; compare versions | J-10 |
| 6. Export branded PDF/DOCX + XLSX forecast | J-11 |
| 7. Consolidated cost + usage CSV | J-12 |
| 8. Rate-limit event with automatic retry/backoff | J-08 (+ J-09 caching) |
| 9. Backup and restore a project | J-14 |
| 10. No crashes / placeholders / raw errors | J-00, J-18, J-19, J-20, J-22 |
| Full live-API acceptance | J-13 |
| Supporting invariants (search, tools, palette, provenance, backends) | J-15, J-16, J-17, J-21, J-23, J-24 |

---

## 6. Proposed implementation layout (when we build these)

```
tests/
  MeticulousResearch.E2E/            <- @e2e @unit journeys: real Core + VMs + FakeChatService
    Fixtures/                        <- sample docs, images, scripted AI streams
    Journeys/                        <- J01_Onboarding.cs ... J24_Provenance.cs
    Support/                         <- E2E host builder, temp-dir harness, fake env/clock
  MeticulousResearch.UiTests/        <- existing FlaUI project; add @e2e journey drivers
    Journeys/                        <- FlaUI variants of the same J-nn flows
```

- **Gate:** `@e2e @unit` journeys run in the headless CI gate
  (`Category!=ui&Category!=manual`) — they are the integration safety net.
- **Nightly/release:** FlaUI `@e2e` journeys (`Category=ui`) drive the real window;
  `@requires-key`/`@requires-network` (J-13) run only when a real key is provided.
- Reuse the existing `FakeChatService`, `FakeClock`, and `FakeEnvironment` from
  `MeticulousResearch.TestSupport`; extend the scripted-stream fixtures as needed.

## 7. Open questions to resolve before implementation

1. **FlaUI automation IDs** — do all journey-critical controls expose stable `AutomationId`s? If
   not, the `@e2e` FlaUI variants need automation-peer wiring first (ties into the `accessibility`
   feature).
2. **Deterministic export bytes** — confirm PDF/DOCX generation is byte-stable (fonts, timestamps
   zeroed) so J-11 byte-identical assertions hold; otherwise assert on structure, not bytes.
3. **Sidecar in CI** — J-21 sidecar-parity and crash-restart: run against a stubbed sidecar in the
   gate, real Node sidecar nightly?
4. **URL fixture** — stand up a loopback HTTP stub for `url-resource` so J-03 stays offline.
5. **Live-API budget** — cap J-13 token spend and pin cheap model tiers to keep the acceptance run
   inexpensive.
```