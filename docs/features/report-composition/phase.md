# Phase — Report Composition

**SPEC:** §3.4.1. **Milestone:** M3. **Depends on:** artifact-creation

## Goal
Let the analyst **order multiple artifacts into a single report** — a report compilation that
references its sections in order and renders as one cohesive document. This is what turns a set of
separately-authored sections (exec summary, sizing, forecast table, competitive landscape) into a
deliverable that `branded-export` (M4) can output as one branded file (§9.1(6)).

## Deliverables
1. **Composition model** — a report compilation is a **document artifact** (reusing the
   artifact-creation domain) marked as a composition, holding an **ordered list of section
   references** to other artifacts. Sections are *references*, not copies.
2. **Composition service** on `IArtifactService` (or a `IReportCompositionService` consuming it):
   - `CreateComposition(projectId, title)`.
   - `AddSection(compositionId, artifactId)` / `RemoveSection`.
   - `ReorderSections(compositionId, orderedSectionIds)`.
   - `PinSectionVersion(compositionId, sectionId, versionId)` — else the section tracks the
     source artifact's current version.
   - `Render(compositionId)` — produce the compiled document content in section order.
3. **Reference tracking** — an unpinned section reflects its source artifact's current version
   live; a pinned section renders a fixed version even as the source advances.
4. **Broken-reference handling** — a section whose source artifact was deleted is flagged;
   rendering skips it with a visible placeholder note (no crash, no silent drop).
5. **Rendering** — concatenate sections in order, each under its artifact title as a heading;
   doc/text/code render as their content; table renders as a table; diagram renders its
   Mermaid source (rendered to image at export time by branded-export).
6. **Views/VMs**: report composition view (§4 screen 5c) — ordered section list with
   drag-to-reorder, add/remove section, pin-version control, empty-state guidance.

## Suggested design
- Persist section order as an ordered join (composition → artifact, with an index and optional
  pinned version_id). Keep it in the existing schema (data-store-migrations owns the schema; add a
  migration there if a link table is needed).
- The composition is itself an artifact so it is listed, versioned, searchable, and exportable
  like any other — reuse artifact-creation rather than inventing a parallel entity.
- `Render` is deterministic and offline; it produces a single document model that `branded-export`
  consumes directly (headings/tables/diagrams carry through, §3.4.2 fidelity).
- Decide the default: sections track **current** version (most useful during iteration); allow
  explicit pinning for a frozen deliverable snapshot. Coordinate version semantics with
  `artifact-versioning`.

## Test-first order
1. Create/add/reorder/remove `@unit` tests → composition model + service.
2. Reference-tracking + pin `@unit` tests → current-vs-pinned rendering.
3. Render-in-order + per-type rendering `@unit` tests → `Render`.
4. Broken-reference + empty-composition `@unit` tests → validation + placeholders.
5. Export hand-off `@unit` test → ordered single-document output.
6. `@ui` tests (ordered list, drag-reorder, add section) → composition view.

## Definition of done
- A composition is a document artifact holding ordered *references* (not copies) to sections.
- Add/remove/reorder work; removing a section leaves the source artifact intact.
- Unpinned sections track the source's current version; pinned sections stay fixed.
- Rendering concatenates sections in order with headings; tables/diagrams carry through.
- Broken references are flagged and skipped with a note; empty compositions guide the user.
- The composition exposes its ordered content as one document for export.

## Notes for later features
- `branded-export` (M4) is the primary consumer: it takes the composed order and applies the
  branded theme (cover, TOC, headers) to export one client-ready file (§3.4.2 / §9.1(6)).
- Coordinate with `artifact-versioning` on how a composition pins/tracks versions.
