# Phase — Branded, Publication-Quality Export

**SPEC:** §3.4.2, §3.7. **Milestone:** M4. **Depends on:** artifact-creation, report-composition

## Goal
Turn an artifact (current version) or a composed report into a **branded, deliverable-grade**
file — Markdown, DOCX, PDF, or XLSX — with cover page, auto TOC, running headers/footers,
consistent styles, and a sources/methodology section. Export is **deterministic, offline, and
previewable** (SPEC §3.4.2).

## Deliverables
1. **`IExportService`** in `MeticulousResearch.Core`:
   - `Preview(source, format, preset, brand, clock)` → an in-memory rendered document (no disk write).
   - `Export(source, format, preset, brand, destination, clock)` → writes the file.
   - `source` = a single artifact's current version **or** a composed report (ordered artifacts).
2. **Format renderers**:
   - **MD** — content passthrough with optional chrome per preset.
   - **DOCX/PDF** — branded theme: cover, TOC with page numbers, running header/footer with
     confidentiality, consistent heading/table/caption styles, sources/methodology section.
   - **XLSX** — tables/forecast models only: preserve typed columns and formulas.
3. **Branded theme engine** — applies accent color + logo + confidentiality from brand settings
   (§3.7), defaulting to a professional navy palette when unset.
4. **Mermaid rendering** — render diagram source to a raster/vector image **offline and
   deterministically**, embedded in DOCX/PDF (no raw Mermaid in the output).
5. **Presets** — `Client-ready report` (full chrome), `Internal draft` (minimal chrome),
   `Plain` (content only).
6. **Preview UI** — the artifact editor's branded export menu shows a preview before save.

## Suggested design
- Keep rendering **pure and deterministic**: inject `IClock` for the cover date, freeze any
  IDs/timestamps in generated metadata, sort deterministically, and avoid embedding wall-clock
  or random values. Two runs on the same input must produce identical bytes (normalize DOCX/PDF
  producer metadata that libraries stamp automatically).
- **No network at export time** — bundle fonts and render Mermaid via a bundled, offline
  renderer (e.g. a local headless renderer or a pre-bundled Mermaid CLI in the sidecar). Assert
  offline in tests.
- Suggested libraries: DOCX via **Open XML SDK**, PDF via a deterministic engine
  (**QuestPDF** or DOCX→PDF), XLSX via **ClosedXML/EPPlus**. Choose ones that allow disabling
  nondeterministic metadata.
- Model the document as an intermediate **document tree** (cover, TOC, sections, blocks) built
  once from the source, then serialized per format — so cover/TOC/headers/styles are shared
  across DOCX and PDF and tested once.
- XLSX maps the table artifact's typed columns and formula cells straight through; reject
  non-tabular sources for XLSX with a clear message.

## Test-first order
1. Format + source-selection `@unit @integration` tests → renderer dispatch + document tree.
2. Branded-theme `@unit @integration` tests (cover, TOC, headers/footers, styles, sources) → theme engine.
3. Mermaid `@unit @integration` tests (rendered image, offline, deterministic) → diagram renderer.
4. XLSX `@unit @integration` tests (types, formulas, non-tabular rejection) → workbook writer.
5. Determinism + offline `@unit @integration` tests → freeze clock/metadata, assert no network.
6. Preview `@unit` + preset `@unit @integration` tests → preview API + preset chrome switches.
7. Branding `@unit @integration` tests (accent, logo, default navy) → theme inputs from Settings.
8. `@ui` (export dialog preview) and `@manual` (publication-quality checklist).

## Definition of done
- All `@unit`/`@integration` scenarios green; two runs on identical input are identical; no network.
- Client-ready DOCX/PDF has cover, TOC with page numbers, headers/footers with confidentiality,
  consistent styles, and a sources/methodology section (§9.1(6)).
- XLSX forecast preserves typed columns and formulas (§9.1(6)).
- Presets switch chrome as specified; preview is shown before any file is written.
- `@manual` branding checklist checked off in the PR.

## Notes for later features
- Brand settings (accent, logo, confidentiality) are read from `settings-secure-key` / Settings
  (§3.7); this feature only consumes them.
- `report-composition` owns the section order; export consumes it read-only.
- `usage-csv-export` is a separate, non-branded data export — do not route it through this theme engine.
