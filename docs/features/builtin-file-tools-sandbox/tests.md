# Tests — Built-in File Tools (Sandboxed)

**SPEC:** §7.4 (built-in tool set), §3.4 (artifact versioning via writes), §3.2.1 (images as vision blocks). **Milestone:** M2.
**Depends on:** ai-gateway

## Traceability
- §7.4 fixed curated tool set: Glob, Grep, Read, Edit, Write + emit_artifact / update_artifact → Tool scenarios.
- §7.4 tools sandboxed to `projects/{projectId}` only; path traversal rejected → Sandbox scenarios.
- §7.4 Edit/Write land as new artifact versions (never silent overwrite) → Write/versioning scenarios.
- §3.2.1 Read returns images as vision content blocks → Read-image scenario.
- §7.4 all tool calls are logged and visible in the conversation → Transparency scenarios.

> The tool set is **closed and curated** — no user-extensible tools/MCP. Tests pin the fixed set,
> the sandbox boundary, and transparency. Uses `FakeChatService` to drive scripted tool calls;
> filesystem uses a temp project dir per test. No network.

---

```gherkin
Feature: Built-in file tools sandbox
  As the app
  I want the model to read/search/write only within a project's sandbox
  So that grounding and authoring are powerful but safe and transparent
```

### The fixed curated tool set (§7.4)

```gherkin
@unit
Scenario: Exactly the curated tools are exposed to the model loop
  Given a conversation generation in project "P"
  When the tool set is provided to the model
  Then it contains exactly: Glob, Grep, Read, Edit, Write, emit_artifact, update_artifact
  And no other tools are available

@unit @integration
Scenario: Glob finds files by pattern within the project sandbox
  Given project "P" with resource files "filing.pdf" and "notes.txt"
  When the model calls Glob with pattern "*.txt"
  Then the result lists "notes.txt"
  And does not list files outside the project sandbox

@unit @integration
Scenario: Grep searches content across resource text and artifact versions
  Given project "P" containing the phrase "market share" in a resource
  When the model calls Grep for "market share"
  Then the match from that resource is returned

@unit @integration
Scenario: Read returns a resource's extracted text
  Given project "P" with a resource whose extracted text is "TAM is $12B"
  When the model calls Read on that resource
  Then it returns "TAM is $12B"

@unit @integration
Scenario: Read returns an image resource as a vision content block
  Given project "P" with an image resource
  When the model calls Read on the image
  Then the result is an image content block (not raw bytes as text)
```

### Writes land as artifact versions (§7.4 / §3.4)

```gherkin
@unit @integration
Scenario: Write creates a new artifact rather than overwriting a file
  Given project "P"
  When the model calls Write to author a document
  Then a new artifact version is created via the artifact service
  And no existing file is silently overwritten

@unit @integration
Scenario: Edit on an existing artifact creates a new version, preserving the prior one
  Given project "P" with an artifact at version 1
  When the model calls Edit on that artifact
  Then a new version 2 is created
  And version 1 still exists unchanged

@unit
Scenario: emit_artifact / update_artifact go through the artifact service contract
  Given the model calls emit_artifact with a title and content
  Then the artifact service receives a structured create request
  And a subsequent update_artifact is received as a structured update
```

### Sandboxing (§7.4)

```gherkin
@unit @integration
Scenario Outline: Path traversal outside the project sandbox is rejected
  Given a tool call in project "P"
  When the model targets the path "<path>"
  Then the call is rejected with a sandbox-violation error
  And nothing outside "projects/P" is read or written

  Examples:
    | path                          |
    | ../otherproject/secret.txt    |
    | ../../Windows/System32/x.dll  |
    | /db.sqlite                    |
    | C:\Users\me\Documents\a.docx  |

@unit @integration
Scenario: Tools cannot reach another project's directory
  Given projects "P" and "Q"
  When a tool call in project "P" targets a file in "projects/Q"
  Then the call is rejected
  And "Q" is untouched

@unit @integration
Scenario: Tools cannot touch the SQLite database or app files
  Given a tool call in project "P"
  When it targets "db.sqlite" or a path outside any project
  Then the call is rejected
```

### Transparency (§7.4)

```gherkin
@unit
Scenario: Every tool call is logged and made visible in the conversation
  Given a generation that calls Grep then Write
  When the turn completes
  Then the conversation records the Grep call and the Write call
  And each is visible to the user with its inputs and outcome

@ui
Scenario: Tool calls appear inline in the thread
  Given the model used Read and Write during a turn
  Then the thread shows the tool activity for that turn
```
