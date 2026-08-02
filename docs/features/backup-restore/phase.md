# Phase — Project Backup & Restore

**SPEC:** §8, §9.1(9). **Milestone:** M4. **Depends on:** projects-crud, data-store-migrations

## Goal
Make a project **self-contained and portable**: back up a single project to a zip (DB subset +
files) and restore it into any install (SPEC §8). This is how hand-off and archiving work in a
local-first, single-user app (SPEC §1.2).

## Deliverables
1. **`IProjectBackupService`** in `MeticulousResearch.Core`:
   - `Backup(projectId, destinationZip)` — writes a zip containing the project's DB subset and
     its on-disk files.
   - `Restore(sourceZip, conflictPolicy)` — recreates the project (rows + files); returns the
     restored project id.
2. **DB subset** — only the target project's rows across `Project`, `Resource`, `Conversation`,
   `Message`, `Artifact`, `ArtifactVersion` (and FTS is rebuilt on restore, not shipped). No
   rows from other projects; **no vault secrets** (§7.5).
3. **File payload** — the project's `resources/{id}/original.*` and `extracted.txt` per the
   §5 layout, packaged preserving relative paths.
4. **Manifest** — a small manifest in the zip recording the **schema/format version** and
   project id, used to validate and to migrate-or-refuse on restore.
5. **Conflict handling** — on restore of an id that already exists: prompt/return a choice of
   **restore-as-copy** (new id) or **replace**; never silently overwrite.
6. **Validation** — reject corrupt/non-project zips with a clear error and leave the store
   unchanged (transactional restore).
7. **UI hooks** — "Back up project" (project menu) and "Restore project" (Projects home).

## Suggested design
- Reuse `IProjectFileStore` (owned by `data-store-migrations`) for the on-disk layout on both
  sides; do not hand-roll paths.
- Serialize the DB subset as portable rows (e.g. JSON or a detached SQLite subset) rather than
  copying raw DB pages, so it survives schema migration. Keep **token columns verbatim** so
  cost recomputes identically (cost is not serialized — `cost-tracking` recomputes from tokens).
- Wrap restore in a transaction + a temp staging dir; commit files and rows together, roll back
  on any failure so a corrupt zip can't half-apply.
- Rebuild FTS entries on restore from the restored content (FTS tables are owned by
  `data-store-migrations`).
- Make backup deterministic where practical (stable entry ordering, fixed clock for the
  manifest) so identical input yields a reproducible archive.
- Exclude the credential vault entirely; the restoring machine supplies its own API key.

## Test-first order
1. Backup `@unit @integration` tests (single zip, DB subset scoped to project, files, no secrets, version) → `Backup`.
2. Restore `@unit @integration` tests (recreate project, files, version history, token counts) → `Restore`.
3. Round-trip + isolation `@unit @integration` tests → end-to-end fidelity + no cross-project clobber.
4. Conflict + corrupt + newer-version `@unit @integration` tests → conflict policy, validation, transactional safety.
5. `@ui` tests (back up from project menu, restore from Projects home) → view wiring.

## Definition of done
- All `@unit @integration` scenarios green against a temp data dir; no network.
- Backup → restore round-trips a project faithfully (fields, resources+blobs, conversations,
  artifacts + version history, token counts) — §9.1(9).
- Restore never overwrites an unrelated project and never half-applies on error.
- Backups contain no vault secrets.

## Notes for later features
- `v1-acceptance` (§9.1(9)) drives a full back-up-and-restore as part of the release bar.
- If future cross-device sync is ever added (SPEC §10, deferred), it can build on this zip
  format + manifest rather than replacing it.
