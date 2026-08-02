# Tests — v1.0 Acceptance (the quality bar)

**SPEC:** §9.1 (v1.0 acceptance criteria 1–10). **Milestone:** M6.
**Depends on:** everything (installer, app-branding-icon, update-notice, and all M0–M5 features)

This is the **final gate**. Each scenario below is one of the ten numbered §9.1 acceptance
criteria, written as a single end-to-end **user journey on a clean Windows 11 machine**. Per
TESTING-STRATEGY §2, this feature is the one place a **live end-to-end run against the real API
is acceptable** (`@requires-network` / `@requires-key`); the criteria are otherwise verified as
`@ui` (real window) or `@manual` (clean-VM, packaging, visual). Unit-level rules for each
capability live in their own feature's `tests.md` — here we prove the whole workflow holds
together.

## Traceability (1:1 to SPEC §9.1)
- §9.1(1) install via signed installer → branded onboarding → **Scenario: 1 — Install and first launch**.
- §9.1(2) validate API key; create project from a deliverable template → **Scenario: 2 — Key validation and project from template**.
- §9.1(3) add mixed resources (PDF, DOCX, XLSX, URL, image); extracted/previewed/token-estimated → **Scenario: 3 — Mixed resources**.
- §9.1(4) grounded, streaming conversation with model selection and per-turn cost → **Scenario: 4 — Grounded streaming chat**.
- §9.1(5) generate Market Research Report from template; iterate with "Edit with Claude"; compare versions → **Scenario: 5 — Report artifact, edit, version compare**.
- §9.1(6) export branded PDF/DOCX (cover, TOC, headers) and an XLSX forecast → **Scenario: 6 — Branded export**.
- §9.1(7) consolidated project cost; export usage CSV → **Scenario: 7 — Cost and usage CSV**.
- §9.1(8) rate-limit event with automatic retry/backoff without losing work → **Scenario: 8 — Rate-limit resilience**.
- §9.1(9) back up and restore a project → **Scenario: 9 — Backup and restore**.
- §9.1(10) no crashes, no unstyled/placeholder screens, no raw errors → **Scenario: 10 — No crashes, no placeholders, no raw errors**.

---

```gherkin
Feature: v1.0 acceptance — clean-machine source-to-deliverable workflow
  As a new analyst on a fresh Windows 11 machine
  I want to go from install all the way to a branded, exportable research deliverable
  So that the app meets the v1.0 quality bar and is shippable

Background:
  Given a clean Windows 11 (x64) machine with no prior install and no app data
```

### 1 — Install and first launch (§9.1.1)

```gherkin
@manual
Scenario: 1 — Install via a signed installer and launch to branded onboarding
  Given the signed release installer
  When I install the app and launch it from the Start Menu
  Then Windows shows a verified publisher (no unknown-publisher warning)
  And the app opens to the branded first-run onboarding welcome (product name, app icon, navy palette)
  And the welcome step states the app is local-first and where data lives
  And no crash, placeholder, or default WPF chrome appears
```

### 2 — Key validation and project from template (§9.1.2)

```gherkin
@ui @requires-network @requires-key
Scenario: 2 — Enter and validate an API key, then create a project from a deliverable template
  Given I am on the onboarding API-key step
  When I enter a valid Anthropic API key and choose "Test key"
  Then the key validates and available models are listed
  And the key is stored securely (not in plaintext or SQLite)
  When I finish onboarding and choose "New project" then the "Market Research Report" template
  Then a new research project is created from that template with its section scaffold
  And I land in the project workspace

@ui @requires-network @requires-key
Scenario: 2b — Environment-provided key and endpoint drive live generation
  # Mirrors the real deployment on this machine: key in ANTHROPIC_API_KEY, endpoint in
  # ANTHROPIC_BASE_URL. The acceptance run must succeed through the resolved credentials,
  # not a key typed into the UI.
  Given the environment provides "ANTHROPIC_API_KEY"
  And the environment provides "ANTHROPIC_BASE_URL" pointing at the gateway endpoint
  And no API key is stored in the secure key store
  When onboarding starts
  Then the API-key step reports the key is already provided by the environment
  When I finish onboarding and run a generation
  Then the request reaches the endpoint from "ANTHROPIC_BASE_URL"
  And I receive a streamed response with non-zero usage
  And neither the key nor the endpoint was persisted to SQLite or a settings file
```

### 3 — Mixed resources (§9.1.3)

```gherkin
@ui @requires-network @requires-key
Scenario: 3 — Add mixed resources and see them extracted, previewed, and token-estimated
  Given an open research project
  When I add a PDF, a DOCX, an XLSX, a URL, and an image as resources
  Then each resource is extracted (the image via vision caption) and shows a preview
  And each shows a token estimate
  And the image resource shows a thumbnail with its cached caption
  And every resource is enabled and in scope by default
```

### 4 — Grounded streaming chat (§9.1.4)

```gherkin
@ui @requires-network @requires-key
Scenario: 4 — Hold a grounded, streaming conversation with model selection and per-turn cost
  Given a project with enabled resources
  When I start a conversation, pick a model tier, and ask a question about the sources
  Then the assistant response streams token-by-token
  And the answer is grounded in the in-scope resources
  And the turn shows the model used and a per-turn cost badge (input/output tokens + USD)
  And the conversation header shows a running total cost
  When I ask a follow-up with a different model tier
  Then the new turn records the newly selected model and its own cost
```

### 5 — Report artifact, edit, version compare (§9.1.5)

```gherkin
@ui @requires-network @requires-key
Scenario: 5 — Generate a Market Research Report artifact, iterate with "Edit with Claude," and compare versions
  Given a project with resources in scope
  When I generate a "Market Research Report" artifact from the template
  Then a document artifact is created with the report sections (exec summary, sizing/forecast, competitive landscape, methodology & sources)
  And it is grounded in the in-scope resources
  When I use "Edit with Claude" to refine a section
  Then a new immutable version is created and set current
  When I open the diff between the two versions
  Then I see a side-by-side/inline diff of exactly what changed
```

### 6 — Branded export (§9.1.6)

```gherkin
@ui
Scenario: 6 — Export a branded, client-ready PDF/DOCX and an XLSX forecast
  Given a Market Research Report artifact and a forecast table artifact
  When I export the report with the "Client-ready report" preset to PDF and to DOCX
  Then a preview is shown, then the files are saved
  And each carries a cover page (title, date, project), an auto-generated TOC with page numbers, and running headers/footers
  And headings, tables, and any Mermaid diagrams carry through
  When I export the forecast table to XLSX
  Then the XLSX preserves typed columns (and formulas where present)
  And all export runs locally with no network
```

### 7 — Cost and usage CSV (§9.1.7)

```gherkin
@ui @requires-network @requires-key
Scenario: 7 — See consolidated project cost and export a usage CSV
  Given a project with several conversation turns and artifact generations
  When I open the project dashboard
  Then the consolidated cost panel shows total spend with breakdowns by model, by conversations-vs-artifacts, and by time window
  When I export usage as CSV
  Then a per-turn-row CSV is written whose totals reconcile with the dashboard
```

### 8 — Rate-limit resilience (§9.1.8)

```gherkin
@ui
Scenario: 8 — Experience a rate-limit event and observe automatic retry/backoff without losing work
  Given a generation that receives an HTTP 429 (rate-limited) response
  When the gateway handles it
  Then it retries automatically with exponential backoff + jitter, honoring retry-after
  And the UI shows a clear "retrying…" state with the attempt count (not a failure)
  And when the retry succeeds the generation completes with no lost input or partial work discarded
  And any interrupted stream is persisted/resumable, not silently dropped
```

> Note: driven with the scripted `FakeChatService` (429 then success) so it is deterministic
> and does not depend on provoking a real rate limit; hence `@ui`, not `@requires-network`.

### 9 — Backup and restore (§9.1.9)

```gherkin
@ui
Scenario: 9 — Back up and restore a project
  Given a project with resources, conversations, and artifacts
  When I back it up to a zip
  Then the zip contains the project's DB subset and its files
  When I restore that zip into the app
  Then the restored project has the same resources, conversations, artifacts, and versions
  And provenance (models, prompts, resource scope, costs) is intact
```

### 10 — No crashes, no placeholders, no raw errors (§9.1.10)

```gherkin
@manual
Scenario: 10 — Complete the whole workflow with no crashes, no placeholder screens, and no raw errors
  Given I have performed criteria 1–9 end to end on the clean machine
  Then the app never crashed
  And no screen showed unstyled/default WPF chrome or a placeholder
  And every error I encountered (e.g. missing key, offline, rate limit, extraction failure) was a human-readable message with a recovery action — never a raw stack trace
  And every list/view I reached had a designed empty, loading, or populated state
```
