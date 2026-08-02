# Phase — Deliverable Templates

**SPEC:** §3.4.1, §6.3. **Milestone:** M3. **Depends on:** artifact-creation

## Goal
Ship the **research deliverable template library** — a config-driven set of structured prompts +
section scaffolds + target formats that steer Claude to firm-quality, grounded artifacts out of
the box. Templates are the recommended path for reports (SPEC §3.4 path 3) and appear in both the
New-artifact and New-project flows. The catalog is **config-driven** (editable JSON/Markdown,
same philosophy as the model catalog §6.3) so the firm can add house formats without a rebuild.

## Deliverables
1. **Template catalog loader** in Core — reads a default JSON that ships with the app, merged with
   a Settings override (like §6.3). Each entry declares: `id`, display `name`, `description`,
   `targetType` (an artifact type from artifact-creation), `sectionScaffold`, `generationPrompt`
   (with `{scope}`, `{horizon}`, `{region}` placeholders), and `defaultModelTier`.
2. **The 8 bundled templates** (§3.4.1 table): Market Research Report (flagship, doc), Executive
   Summary / Brief (doc), Competitive Landscape (table), Market Forecast Model (table),
   SWOT / Porter's Five Forces (doc), Company / Vendor Profile (doc), Customer / Buyer Insights
   (doc), Trend / Technology Scan (doc). Sections per the table's "Typical sections".
3. **Prompt assembler** — substitutes placeholders (scope/horizon/region, with sensible defaults
   e.g. region→"Global"), and prepends **grounding-first** instructions: cite which in-scope
   resource supports each claim; flag unsupported assertions. In-scope = enabled resources only.
4. **Template-driven generation** — `GenerateFromTemplate(projectId, templateId, params)` builds
   the request and calls `IArtifactService.Generate` (artifact-creation); the resulting version
   records the template id, assembled prompt, model, in-scope resources, and usage.
5. **Validation** — malformed catalog entries (missing required field) produce a descriptive
   error identifying the bad entry; valid entries still load.
6. **Views/VMs**: template gallery (name/description/preview) surfaced in the New-artifact flow
   and the New-project flow; "New project from template" seeds a first artifact.

## Suggested design
- Keep the catalog schema parallel to the model catalog (§6.3): default resource + Settings
  override path; treat JSON as the source of truth.
- Store the section scaffold as ordered heading strings (or a small Markdown scaffold) so the
  gallery preview and the generation prompt can both use it.
- The grounding-first instruction block is a shared preamble applied to every template's prompt,
  not duplicated per template — one place to tune "cite sources / flag unsupported claims."
- Generation goes through the artifact-creation `Generate` seam; in tests, `FakeChatService`
  returns deterministic content (e.g. echoing scaffold headings) so scenarios are stable
  (TESTING-STRATEGY §4). No live API.
- "New project from template" composes projects-crud `Create` + `GenerateFromTemplate`.

## Test-first order
1. Catalog load + override + malformed-entry `@unit` tests → loader + validation.
2. Bundled-templates presence + field-declaration `@unit` tests → the 8 default entries.
3. Placeholder substitution + defaults `@unit` tests → prompt assembler.
4. Grounding-first + enabled-only-scope `@unit` tests → shared preamble + scope filter.
5. Generate-from-template + flagship `@unit` tests → `GenerateFromTemplate` over FakeChatService.
6. New-project-from-template `@unit` test → compose with projects-crud.
7. `@ui` gallery tests (New-artifact, New-project) → gallery views.

## Definition of done
- All 8 bundled templates load from config with correct target types and required fields.
- Placeholders substitute; unfilled optionals fall back; no unresolved placeholders remain.
- Every assembled prompt carries grounding-first instructions; only enabled resources are in scope.
- Generating from a template produces a correctly-typed artifact whose version records the
  template id, prompt, model, in-scope resources, and usage — via FakeChatService in tests.
- The gallery appears in both New-artifact and New-project flows with previews.

## Notes for later features
- `edit-with-claude` iterates on template-generated artifacts; the grounding preamble carries into
  follow-up prompts where relevant.
- `branded-export` (M4) exports template-produced artifacts; the section scaffold maps to headings.
- Post-v1 "firm template pack" (§10.7) extends this same config system with house style.
- Template management UI lives in Settings (§4.7) — this feature owns the catalog it edits.
