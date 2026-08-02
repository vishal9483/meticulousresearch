# Tests — Project Backup & Restore

**SPEC:** §8 (backup/restore of a project as a zip — db subset + files), §9.1(9). **Milestone:** M4.
**Depends on:** projects-crud (project domain + file layout), data-store-migrations (schema, file store)

## Traceability
- §8 export/backup of a project as a zip (db subset + files) → Backup scenarios.
- §8 restore a project from a zip → Restore scenarios.
- §5 per-project file layout (resources/blobs/extracted text) → Round-trip scenarios.
- §9.1(9) back up and restore a project → covered here.

> **Determinism & filesystem (TESTING-STRATEGY §4):** backup/restore is `@unit @integration`;
> it touches SQLite + the filesystem via a temp data dir per test and makes **no network call**.
> A backup written from fixed input is reproducible under a fixed clock.

---

```gherkin
Feature: Project backup & restore
  As an analyst
  I want to back up a project to a zip and restore it later
  So that I can archive and hand off a self-contained project safely
```

## Background

```gherkin
Background:
  Given a project "EV Market 2026" with:
    | resources | conversations | artifacts |
    | 3         | 2             | 1         |
  And each resource has an original blob and extracted text on disk
  And each artifact has an ordered version history
```

### Backup

```gherkin
@unit @integration
Scenario: Backing up a project writes a single zip
  Given the project "EV Market 2026"
  When I back it up to "ev-market.zip"
  Then a single zip file "ev-market.zip" is written

@unit @integration
Scenario: The backup contains the project's DB subset
  Given the project "EV Market 2026"
  When I back it up
  Then the zip contains the project's rows for Project, Resource, Conversation, Message, Artifact, and ArtifactVersion
  And it contains no rows belonging to other projects

@unit @integration
Scenario: The backup contains the project's files
  Given the project "EV Market 2026" with resource blobs and extracted text on disk
  When I back it up
  Then the zip contains each resource's original blob and extracted text
  And it preserves the per-project file layout

@unit @integration
Scenario: App-level secrets are not included in a backup
  Given an API key stored in the credential vault
  When I back up the project "EV Market 2026"
  Then the zip contains no API key or other vault secret

@unit @integration
Scenario: A backup carries a schema/format version
  Given the project "EV Market 2026"
  When I back it up
  Then the zip records the schema version it was produced at
```

### Restore

```gherkin
@unit @integration
Scenario: Restoring a backup recreates the project
  Given a backup zip of "EV Market 2026"
  And a data store that does not contain that project
  When I restore from the zip
  Then a project "EV Market 2026" exists
  And it has 3 resources, 2 conversations, and 1 artifact

@unit @integration
Scenario: Restore recreates resource blobs and extracted text on disk
  Given a backup zip of "EV Market 2026"
  When I restore from the zip
  Then each resource's original blob and extracted text exist on disk under the project's file layout

@unit @integration
Scenario: Restore preserves artifact version history and current version
  Given a backup zip of a project whose artifact has 3 versions
  When I restore from the zip
  Then the artifact has all 3 versions in order
  And its current version pointer is preserved

@unit @integration
Scenario: Restore preserves token counts so cost recomputes
  Given a backup zip of a project with per-turn token counts
  When I restore from the zip
  Then the restored turns carry the same token counts
  And the consolidated cost recomputes to the same value under the same price table
```

### Round-trip & integrity

```gherkin
@unit @integration
Scenario: A backup then restore round-trips the project faithfully
  Given the project "EV Market 2026"
  When I back it up and restore it into an empty data store
  Then the restored project matches the original in fields, resources, conversations, artifacts, and versions

@unit @integration
Scenario: Restoring does not overwrite an unrelated existing project
  Given a data store containing a project "Healthcare 2026"
  And a backup zip of "EV Market 2026"
  When I restore from the zip
  Then "Healthcare 2026" is unchanged
  And both projects now exist

@unit @integration
Scenario: Restoring a backup whose id already exists is handled without clobbering
  Given a data store already containing "EV Market 2026"
  And a backup zip of "EV Market 2026"
  When I restore from the zip
  Then I am prompted to restore as a copy or replace
  And no data is silently overwritten

@unit @integration
Scenario: A corrupt or non-project zip is rejected with a clear error
  Given a zip that is not a valid project backup
  When I try to restore from it
  Then restore fails with a clear, human-readable error
  And the data store is left unchanged

@unit @integration
Scenario: A backup from a newer schema version is refused or migrated, never partially applied
  Given a backup zip produced at a newer schema version than this app
  When I try to restore from it
  Then restore either migrates it forward or refuses with a clear message
  And the data store is left consistent
```

### UI

```gherkin
@ui
Scenario: Backing up a project from the project menu
  Given the project "EV Market 2026" is open
  When I choose "Back up project" and pick a destination
  Then a backup zip is written
  And a confirmation is shown

@ui
Scenario: Restoring a project from the Projects home
  Given the Projects home is open
  When I choose "Restore project" and pick a backup zip
  Then the restored project appears in the Projects list
```
