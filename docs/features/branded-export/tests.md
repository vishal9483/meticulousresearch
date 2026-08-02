# Tests — Branded, Publication-Quality Export

**SPEC:** §3.4.2 (branded export), §3.7 (brand accent/logo). **Milestone:** M4.
**Depends on:** artifact-creation (artifact + current version), report-composition (composed report order)

## Traceability
- §3.4.2 formats MD/DOCX/PDF/XLSX using current version or composed report order → Format scenarios.
- §3.4.2 branded theme (cover page, auto TOC with page numbers, running headers/footers with confidentiality, consistent heading/table/caption styles, sources/methodology section) → Branded-theme scenarios.
- §3.4.2 Mermaid rendered to images → Diagram scenarios.
- §3.4.2 XLSX preserves typed columns and formulas → XLSX scenarios.
- §3.4.2 deterministic + offline + preview before save → Determinism / Offline / Preview scenarios.
- §3.4.2 presets Client-ready / Internal draft / Plain → Preset scenarios.
- §3.7 configurable accent + logo from Settings → Branding scenarios.
- §9.1(6) branded client-ready PDF/DOCX (cover, TOC, headers) + XLSX forecast → covered here.

> **Determinism & offline (TESTING-STRATEGY §4):** every export scenario asserts that the same
> input produces byte-identical (or normalized-identical) output and that **no network call**
> occurs. Export is `@unit`/`@integration`; visual polish of the rendered document is `@manual`.

---

```gherkin
Feature: Branded, publication-quality export
  As an analyst
  I want to export an artifact or a composed report as a branded, client-ready file
  So that I can deliver a professional deliverable without any manual formatting
```

## Background

```gherkin
Background:
  Given a project "EV Market 2026" with brand settings:
    | accent | logo               | confidentiality        |
    | navy   | assets/firm.png    | Confidential — Meticulous |
  And an artifact "Market Report" whose current version contains headings, a table, a list, a code block, and a Mermaid diagram
```

### Formats & source selection

```gherkin
@unit @integration
Scenario Outline: Exporting the current version to each supported format
  Given the "Market Report" artifact
  When I export it as "<format>"
  Then a "<format>" file is produced
  And its content is derived from the artifact's current version
  Examples:
    | format |
    | MD     |
    | DOCX   |
    | PDF    |
    | XLSX   |

@unit @integration
Scenario: Exporting a composed report uses the section order
  Given a composed report ordering artifacts "Summary", "Sizing", "Landscape"
  When I export the composed report as "PDF"
  Then the exported document contains those sections in that exact order

@unit
Scenario: A table/dataset artifact is the source for XLSX export
  Given a table artifact "Forecast Model" with typed columns and a formula column
  When I export it as "XLSX"
  Then the workbook is produced from that artifact's current version
```

### Branded document theme (DOCX / PDF)

```gherkin
@unit @integration
Scenario: A client-ready export has a cover page
  Given the "Market Report" artifact
  When I export it as "PDF" with the "Client-ready report" preset
  Then the document has a cover page with the report title, subtitle, date, project, and firm logo

@unit @integration
Scenario: A client-ready export has an auto table of contents with page numbers
  Given the "Market Report" artifact with multiple headings
  When I export it as "DOCX" with the "Client-ready report" preset
  Then a table of contents is generated from the headings
  And each TOC entry has a page number

@unit @integration
Scenario: Running headers and footers carry the title, page number, and confidentiality
  Given the "Market Report" artifact
  When I export it as "PDF" with the "Client-ready report" preset
  Then each page has a running header or footer with the report title
  And each page shows a page number
  And each page shows the confidentiality notice "Confidential — Meticulous"

@unit @integration
Scenario: Headings, tables, lists, captions, and code blocks carry through with consistent styles
  Given the "Market Report" artifact
  When I export it as "DOCX"
  Then its headings, tables, lists, captions, and code blocks are present
  And they use the branded style set (consistent heading, table, and caption styles)

@unit @integration
Scenario: A sources / methodology section is included
  Given the "Market Report" artifact with cited sources
  When I export it as "PDF" with the "Client-ready report" preset
  Then the document contains a sources / methodology section
```

### Mermaid diagrams rendered to images

```gherkin
@unit @integration
Scenario: A Mermaid diagram is rendered to an image in DOCX/PDF
  Given the "Market Report" artifact containing a Mermaid diagram
  When I export it as "PDF"
  Then the diagram appears as a rendered image, not as raw Mermaid source

@unit @integration
Scenario: Diagram rendering is offline and deterministic
  Given the "Market Report" artifact containing a Mermaid diagram
  When I export it twice with no network available
  Then both exports render the diagram identically
  And no network request is made
```

### XLSX fidelity

```gherkin
@unit @integration
Scenario: XLSX preserves typed columns
  Given a table artifact with a text column, a number column, and a date column
  When I export it as "XLSX"
  Then each column cell carries its declared type (text, number, date)

@unit @integration
Scenario: XLSX preserves formulas where present
  Given a forecast table with a CAGR column defined by a formula
  When I export it as "XLSX"
  Then the formula cells contain the formula, not just a static value

@unit @integration
Scenario: Non-tabular content cannot be exported to XLSX
  Given a prose document artifact with no table
  When I try to export it as "XLSX"
  Then I am told XLSX requires a table/dataset artifact
  And no file is produced
```

### Determinism & offline

```gherkin
@unit @integration
Scenario Outline: The same input produces the same output on repeat export
  Given the "Market Report" artifact and fixed brand settings
  When I export it as "<format>" twice with a fixed clock
  Then the two outputs are identical
  Examples:
    | format |
    | MD     |
    | DOCX   |
    | PDF    |
    | XLSX   |

@unit @integration
Scenario: Export makes no network calls
  Given the "Market Report" artifact
  When I export it as "PDF"
  Then no network request is made during export

@unit
Scenario: A fixed clock produces a stable cover date
  Given the clock is set to "2026-08-03"
  When I export the "Market Report" artifact as "PDF" with the "Client-ready report" preset
  Then the cover date reads "2026-08-03"
```

### Preview before save

```gherkin
@unit
Scenario: A preview is produced before the file is written to disk
  Given the "Market Report" artifact
  When I request a "PDF" export preview
  Then a preview of the branded document is produced
  And nothing is written to the export destination yet

@ui
Scenario: The export dialog shows a preview before saving
  Given the artifact editor is open on "Market Report"
  When I open the branded export menu and choose "PDF"
  Then a preview of the branded document is shown
  And I can confirm to save or cancel without writing a file
```

### Export presets

```gherkin
@unit @integration
Scenario Outline: Presets control the amount of chrome
  Given the "Market Report" artifact
  When I export it as "PDF" with the "<preset>" preset
  Then the cover page is <cover>
  And the table of contents is <toc>
  And the running header/footer is <chrome>
  Examples:
    | preset               | cover   | toc     | chrome  |
    | Client-ready report  | present | present | present |
    | Internal draft       | absent  | absent  | minimal |
    | Plain                | absent  | absent  | absent  |

@unit
Scenario: The Plain preset emits content only
  Given the "Market Report" artifact
  When I export it as "MD" with the "Plain" preset
  Then the output contains the artifact content
  And it contains no cover page, TOC, header, or footer chrome
```

### Configurable brand accent & logo

```gherkin
@unit @integration
Scenario: The configured accent color is applied to the branded theme
  Given the brand accent is set to "navy" in Settings
  When I export the "Market Report" artifact as "PDF" with the "Client-ready report" preset
  Then the branded accent used in the document is "navy"

@unit @integration
Scenario: The configured logo is placed on the cover and/or header
  Given a firm logo configured in Settings
  When I export the "Market Report" artifact as "PDF" with the "Client-ready report" preset
  Then the firm logo appears on the cover page

@unit @integration
Scenario: A default professional navy palette is used when no accent is configured
  Given no brand accent is configured
  When I export the "Market Report" artifact as "PDF" with the "Client-ready report" preset
  Then the default navy corporate accent is applied

@manual
Scenario: The client-ready PDF looks like a professional firm deliverable
  Given a client-ready PDF export of a full Market Research Report
  When a reviewer inspects it against the branding checklist
  Then the cover, TOC, headers/footers, typography, and tables read as publication-quality
```
