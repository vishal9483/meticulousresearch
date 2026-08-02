# Tests — Data Store & Migrations

**SPEC:** §5 (data model), §8 (SQLite WAL, integrity). **Milestone:** M0.
**Depends on:** —

## Traceability
- §5 schema (Project/Resource/Conversation/Message/Artifact/ArtifactVersion/Setting) → Schema scenarios.
- §5 FTS5 virtual tables → Full-text tables scenario.
- §8 WAL mode + integrity check → Reliability scenarios.
- §5 files-on-disk layout → Data directory scenario.

---

```gherkin
Feature: Local data store & schema migrations
  As the application
  I need a versioned SQLite schema and a per-project file layout
  So that all metadata and blobs persist reliably and upgrade safely
```

### Fresh install

```gherkin
@unit @integration
Scenario: A fresh database is created with the current schema
  Given no database file exists in the data directory
  When the data store initializes
  Then a "db.sqlite" file is created
  And the schema version equals the latest migration version
  And WAL journal mode is enabled

@unit @integration
Scenario Outline: All core tables exist after initialization
  Given an initialized data store
  Then a table named "<table>" exists
  Examples:
    | table           |
    | Project         |
    | Resource        |
    | Conversation    |
    | Message         |
    | Artifact        |
    | ArtifactVersion |
    | Setting         |

@unit @integration
Scenario Outline: Full-text search virtual tables exist
  Given an initialized data store
  Then an FTS5 virtual table indexing "<content>" exists
  Examples:
    | content                   |
    | resource extracted text   |
    | message content           |
    | artifact version content  |
```

### Schema shape (columns that later features rely on)

```gherkin
@unit @integration
Scenario: Message table records token and cost columns
  Given an initialized data store
  Then the "Message" table has columns tokens_in, tokens_out, tokens_cache_read, tokens_cache_write, cost_usd, model, latency_ms, resource_scope_json

@unit @integration
Scenario: ArtifactVersion table records provenance columns
  Given an initialized data store
  Then the "ArtifactVersion" table has columns version_no, content, content_format, model, prompt, tokens_in, tokens_out, cost_usd, resource_scope_json, created_by
```

### Migrations

```gherkin
@unit @integration
Scenario: An out-of-date database is upgraded in place
  Given a database at an older schema version with existing rows
  When the data store initializes
  Then all pending migrations run in order
  And the schema version equals the latest migration version
  And pre-existing rows are preserved

@unit @integration
Scenario: Migrations are idempotent on an up-to-date database
  Given a database already at the latest schema version
  When the data store initializes again
  Then no migration runs
  And no data changes
```

### Reliability & layout

```gherkin
@unit @integration
Scenario: Integrity check passes on a healthy database
  Given an initialized data store
  When an integrity check runs
  Then it reports "ok"

@unit @integration
Scenario: The per-project file layout is created on demand
  Given an initialized data store
  When a project with id "P1" needs resource storage
  Then a directory "projects/P1/resources" exists under the data directory

@unit @integration
Scenario: Data directory location is configurable
  Given a configured data directory at a custom path
  When the data store initializes
  Then the database and projects folder are created under that custom path
```
