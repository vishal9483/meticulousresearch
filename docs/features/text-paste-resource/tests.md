# Tests — Text Paste Resource

**SPEC:** §3.2 (resources — text paste; base resource model & add/preview flow). **Milestone:** M1.
**Depends on:** projects-crud

## Traceability
- §3.2 types — *text paste* (arbitrary text captured inline) → Add text scenarios.
- §3.2 per-resource fields (title, type, source, extracted text, byte size, token estimate, enabled, added timestamp) → Resource fields scenario.
- §3.2 extraction pipeline (store extracted text in project files dir) → Storage scenarios.
- §3.2 preview extracted text → Preview scenario.
- §9.1(3) add resources and see them extracted, previewed, and token-estimated → covered here for the text type (file/URL/image in sibling features).

---

```gherkin
Feature: Text paste resources
  As an analyst
  I want to paste arbitrary text into a project as a resource
  So that Claude can be grounded in my own notes and excerpts
```

```gherkin
Background:
  Given a project "Semiconductors 2026" is open
```

### Adding pasted text

```gherkin
@unit
Scenario: Pasting text creates an enabled resource
  Given the "Add resource" menu is open
  When I paste "Global foundry capacity grew 12% in 2025." with title "Foundry note"
  Then a resource "Foundry note" of type "text" exists in the project
  And it is enabled
  And its added timestamp is set

@unit
Scenario: A pasted resource stores its text as extracted text
  Given I paste "Wafer starts rose sharply." with title "Wafer note"
  When the resource is saved
  Then its extracted text is "Wafer starts rose sharply."
  And the extracted text is written under "projects/{projectId}/resources/{resourceId}/extracted.txt"

@unit
Scenario: A pasted text resource has no original blob
  Given I paste "Inline snippet." with title "Snippet"
  When the resource is saved
  Then its blob_path is empty
  And its source_uri is empty

@unit
Scenario: Title defaults from the first line when omitted
  Given I paste "Market summary\nrest of the text" with no title
  When the resource is saved
  Then its title is "Market summary"

@unit
Scenario: Pasting empty text is rejected
  Given the "Add resource" menu is open
  When I try to paste text that is empty or whitespace only
  Then I see an inline validation error
  And no resource is created
```

### Resource fields & sizing

```gherkin
@unit
Scenario: A text resource records byte size and a token estimate
  Given I paste "Global foundry capacity grew 12% in 2025." with title "Foundry note"
  When the resource is saved
  Then its byte_size equals the UTF-8 byte length of the text
  And its token_estimate is a positive number

@unit
Scenario: A saved resource re-reads all fields after reopening the project
  Given I paste "Persisted text." with title "Persisted"
  When I close and reopen the project
  Then the resource "Persisted" is present with type "text", enabled true, and its extracted text intact
```

### Preview

```gherkin
@unit
Scenario: Previewing a text resource shows its extracted text
  Given a text resource "Foundry note" with text "Global foundry capacity grew 12% in 2025."
  When I preview the resource
  Then the preview shows "Global foundry capacity grew 12% in 2025."

@ui
Scenario: Adding a pasted resource shows it in the resources table
  Given the Resources view is open
  When I add a pasted resource "Foundry note"
  Then the resources table lists "Foundry note" with type "Text" and an enabled toggle

@ui
Scenario: Selecting a text resource shows its extracted text in the preview pane
  Given a text resource "Foundry note" exists
  When I select it in the resources table
  Then the preview pane shows its extracted text
```
