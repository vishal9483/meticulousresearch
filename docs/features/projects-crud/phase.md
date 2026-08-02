# Phase — Projects CRUD

**SPEC:** §3.1. **Milestone:** M0. **Depends on:** data-store-migrations, app-shell-navigation

## Goal
Full lifecycle management of research projects and the project dashboard — the "project as the
unit of work" (SPEC §1.3). Owns the **project domain model + service** and the Projects home /
dashboard views.

## Deliverables
1. **`IProjectService`** in Core: `Create`, `Rename`, `Duplicate`, `Archive`/`Unarchive`,
   `Delete`, `Get`, `List(includeArchived)`, `Search(query)`, `GetDashboard(projectId)`.
2. **Domain model** matching §5 `Project` (name, description, custom_instructions, default_model,
   color, archived, created_at, updated_at).
3. **Dashboard aggregation** — counts (resources/conversations/artifacts), last activity.
4. **Views/VMs**: Projects home (grid/list of cards, search, archived toggle, designed empty
   state) and Project dashboard (counts, last activity, quick actions). Wire into the shell nav.
5. **Custom-instructions accessor** used later by context assembly (expose on the project so
   `conversations`/artifact generation can inject it into the system prompt).

## Suggested design
- `Duplicate` copies project row + resource rows + resource blobs on disk, but NOT conversations
  or artifacts (per test). Reuse `IProjectFileStore` to copy files.
- `Delete` removes DB rows (cascade) and the `projects/{id}` directory; require confirmation in UI.
- `Search` is a simple name/description filter here; project-wide FTS across content is the
  separate `full-text-search` feature.
- Default model comes from `ISettingsService` at create time.
- Timestamps via injected `IClock`.

## Test-first order
1. Create/validation/fields `@unit` tests → domain + `Create` + custom-instructions accessor.
2. Rename/duplicate/archive/delete `@unit` tests → service methods + file handling.
3. Dashboard + search `@unit` tests → aggregation + filter.
4. `@ui` tests (open workspace on create, confirm-on-delete, empty state, quick actions) → views.

## Definition of done
- All CRUD, dashboard, and search `@unit` scenarios green; `@ui` scenarios green.
- Duplicate copies config+resources only; delete removes files; archive toggles list visibility.
- Projects home has a designed empty state (no blank screen).

## Notes for later features
- `deliverable-templates` adds the "New project from template" path on top of `Create`.
- `backup-restore` serializes/deserializes a project (DB subset + files) — coordinate on the
  file layout owned by data-store-migrations.
- Dashboard's consolidated **cost panel** is added later by `cost-tracking`; leave a slot for it.
