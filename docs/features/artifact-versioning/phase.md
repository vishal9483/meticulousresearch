# Phase — Artifact Versioning

**SPEC:** §3.4, §5. **Milestone:** M3. **Depends on:** artifact-creation

## Goal
Own the **version-history semantics** of artifacts: every edit or regeneration is a new immutable
version, versions form an ordered history, and the analyst can set-current, revert, duplicate,
delete, and promote-to-resource. This realizes the "nothing is lost" principle (SPEC §1.3) and
provides the version substrate that `artifact-diff` and `edit-with-claude` build on.

## Deliverables
1. **Versioning API** on `IArtifactService` (extending the seam from artifact-creation):
   - `AddVersion(artifactId, content, provenance)` — the single entry for all changes; assigns the
     next `version_no`, writes immutably, updates `current_version_id`.
   - `GetHistory(artifactId)` — ordered version list (newest-first for display).
   - `SetCurrentVersion(artifactId, versionId)` — repoints current without creating a version.
   - `RevertTo(artifactId, versionId)` — creates a *new* version copying that version's content.
   - `DuplicateArtifact(artifactId, newTitle)` — copies the artifact + full version history.
   - `DeleteArtifact(artifactId)` / `DeleteVersion(artifactId, versionId)`.
   - `PromoteToResource(artifactId, targetProjectId)` — creates an `artifact_ref` resource.
2. **Immutability guarantee** — no in-place mutation of a saved `ArtifactVersion`; every change
   funnels through `AddVersion`. Reject direct overwrites.
3. **Provenance rules** — generated versions record model/prompt/resource_scope/usage/cost;
   manual-edit and revert versions record created_by "user", usage/cost 0, model/prompt null.
4. **Delete rules** — deleting the artifact removes all versions; deleting a single version is
   allowed except the current one (must set another current first).
5. **Views/VMs**: version history rail in the artifact editor (current marked; per-version
   created_at/model/created_by), set-current / revert / duplicate / delete / promote-to-resource
   actions, delete confirmation.

## Suggested design
- `version_no` is per-artifact monotonic (max+1), assigned inside `AddVersion` under the same
  transaction that repoints `current_version_id`, so ordering is race-free.
- `RevertTo` = read target content → `AddVersion` with created_by "user"; it never rewrites
  history, so the timeline stays append-only and auditable.
- `DuplicateArtifact` deep-copies version rows (preserving relative order and provenance) into a
  new artifact id; the copy is fully independent (edits don't cross over).
- `PromoteToResource` writes an `artifact_ref` Resource (schema owned by data-store-migrations,
  type per §3.2) whose extracted text is the current version content, so it is FTS-indexed and
  grounding-eligible; coordinate the resource shape with the resources features (M1).
- `IClock` for timestamps; ensure distinct, ordered created_at even for rapid successive versions
  (tie-break on version_no for display).

## Test-first order
1. Immutability + new-version-on-change `@unit` tests → `AddVersion` + overwrite rejection.
2. Ordered-history + metadata `@unit` tests → `GetHistory` + provenance rules.
3. Set-current + revert `@unit` tests → `SetCurrentVersion`, `RevertTo`.
4. Duplicate `@unit` tests → `DuplicateArtifact` (independent copy).
5. Delete rules `@unit` tests → artifact/version delete + current-version guard.
6. Promote-to-resource `@unit` tests → `PromoteToResource`.
7. `@ui` tests (history rail, delete confirmation) → editor rail + dialogs.

## Definition of done
- Every edit/regeneration creates a new immutable version; saved versions are never mutated.
- History is ordered; each version records model/prompt/in-scope resources/timestamp/usage.
- Set-current, revert (creates a new version), duplicate (independent copy), delete (with
  current-version guard), and promote-to-resource all behave per scenarios.
- History rail shows versions with the current marked; delete asks for confirmation.

## Notes for later features
- `artifact-diff` compares any two versions from `GetHistory`.
- `edit-with-claude` and manual edit both call `AddVersion` — versioning stays the one code path.
- `report-composition` references artifacts (usually their current version); coordinate on how a
  composition pins a specific version vs. tracks "current."
- `branded-export` (M4) exports the current version (or composed order).
