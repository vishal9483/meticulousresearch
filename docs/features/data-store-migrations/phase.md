# Phase — Data Store & Migrations

**SPEC:** §5, §8. **Milestone:** M0. **Depends on:** —

## Goal
Own the **persistence foundation**: SQLite schema, versioned migrations, FTS5 tables, WAL/integrity,
and the per-project on-disk file layout. Every data-bearing feature builds on this.

## Deliverables
1. **Persistence stack** in `MeticulousResearch.Core`:
   - EF Core + `Microsoft.Data.Sqlite` (or Dapper). Pick one and stay consistent; EF Core
     recommended for migrations, with raw SQL for FTS5 virtual tables.
2. **Schema** exactly per SPEC §5: `Project`, `Resource`, `Conversation`, `Message`, `Artifact`,
   `ArtifactVersion`, `Setting`, plus FTS5 virtual tables over resource extracted text, message
   content, and artifact version content.
3. **Migration runner** — versioned, ordered, idempotent; records `schema_version` (e.g. in
   `Setting` or a dedicated table). Runs pending migrations on startup, preserves data.
4. **Connection setup** — WAL mode, foreign keys on, busy-timeout; an `IntegrityCheck()` API
   wrapping `PRAGMA integrity_check`.
5. **File layout service** — `IProjectFileStore` resolving `projects/{projectId}/resources/...`,
   `exports/`, `logs/` under a configurable data directory (default `%LOCALAPPDATA%/MeticulousResearch`).
6. **Repositories / DbContext** exposed via DI for other features to consume.

## Suggested design
- `AppDbContext` (EF Core) for the relational tables; create FTS5 tables + triggers via a raw-SQL
  migration (EF can't model virtual tables). Triggers keep FTS rows in sync with base tables.
- `IClock` injected for `created_at`/`updated_at` (see TESTING-STRATEGY §4).
- `DataStore.Initialize(dataDir)` = ensure dir → open connection → set WAL/PRAGMAs → run migrations.
- Keep the schema-version constant in code so tests can assert "latest".

## Test-first order
1. Fresh-install schema + FTS + column-shape `@unit @integration` tests → implement schema/migrations.
2. Migration upgrade/idempotency tests → implement ordered runner + version tracking.
3. Integrity, WAL, and file-layout tests → implement pragmas + `IProjectFileStore`.

## Definition of done
- All `@unit @integration` scenarios green against a temp data dir.
- Schema matches §5 column-for-column (later features assert specific columns).
- Migrations upgrade an old DB without data loss and are idempotent.

## Notes for later features
- This feature is the **owner of the schema**. Later features that need new columns/tables add a
  **new migration** here (or in their own migration file following this runner's convention) —
  never ad-hoc `ALTER` at runtime.
- `settings-secure-key` uses the `Setting` table for non-secret settings; secrets go to the
  credential vault, not here.
