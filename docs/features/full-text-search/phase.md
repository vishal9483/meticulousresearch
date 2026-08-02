# Phase — Full-Text Search

**SPEC:** §3.1, §5. **Milestone:** M1. **Depends on:** text-paste-resource

## Goal
Provide **SQLite FTS5 full-text search within a project across resources**, kept in sync as
resources are added, re-extracted, and removed. Built so it **extends** to conversation and
artifact content later (the FTS5 tables for those already exist per §5).

## Deliverables
1. **`ISearchService`** in Core: `SearchResources(projectId, query)` returning ranked matches
   scoped to the project. Shaped to add `SearchMessages` / `SearchArtifacts` later without a
   redesign.
2. **FTS5 query layer** — query the resource extracted-text FTS5 virtual table (owned by
   data-store-migrations), project-scoped, case-insensitive, relevance-ranked.
3. **Index sync** — ensure add / re-extract / remove keep the FTS index correct (via the
   data-store triggers; verify with integration tests). No stale or orphaned index rows.
4. **Resources-view search box** — filters the resource list live; designed empty state on no
   matches.

## Suggested design
- Use FTS5 `MATCH` with `rank`/`bm25` ordering; sanitize user input into a safe FTS query
  (escape special syntax) to avoid query-syntax errors.
- Keep FTS DDL/triggers in data-store-migrations; this feature only **reads** and asserts sync.
- Project scoping: join FTS results back to `Resource` filtered by `project_id` (or store
  `project_id` in the FTS content row) so cross-project leakage is impossible.
- Return lightweight hits (resource id, title, maybe a snippet) for the VM to render.

## Test-first order
1. Keyword / body-match / case-insensitive `@unit @integration` tests → query layer.
2. Ranking + project-scoping `@unit @integration` tests → ordering + scope.
3. Add / re-extract / remove sync `@unit @integration` tests → verify trigger-kept index.
4. Empty-result + extensibility `@unit` tests → service shape.
5. Live-filter + empty-state `@ui` test → resources search box.

## Definition of done
- All search `@unit @integration` scenarios green against a temp SQLite DB.
- Results are project-scoped, case-insensitive, relevance-ranked, and stay in sync on
  add/re-extract/remove.
- Service interface is ready to extend to messages/artifacts (M2/M3).

## Notes for later features
- `conversations` (M2) and `artifacts` (M3) plug their content into `SearchMessages` /
  `SearchArtifacts` on the same `ISearchService`.
- The command palette / project search (M5) can call this service for project-wide search.
