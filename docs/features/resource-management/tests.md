# Tests — Resource Management

**SPEC:** §3.2 (resources — management: rename, re-extract, enable/disable, remove, preview, token contribution). **Milestone:** M1.
**Depends on:** text-paste-resource

## Traceability
- §3.2 management — rename → Rename scenarios.
- §3.2 management — re-extract → Re-extract scenarios.
- §3.2 management — enable/disable toggle → Enable/disable scenarios.
- §3.2 management — remove → Remove scenarios.
- §3.2 management — preview extracted text → Preview scenario.
- §3.2 management — see token estimate contribution → Token-contribution scenarios.
- §9.1(3) resources are previewed/managed → covered here.

---

```gherkin
Feature: Managing resources
  As an analyst
  I want to rename, re-extract, toggle, preview, and remove resources
  So that I control exactly what grounds my work and keep it tidy
```

```gherkin
Background:
  Given a project "Semiconductors 2026" with several resources is open
```

### Rename

```gherkin
@unit
Scenario: Renaming a resource updates its title and timestamp
  Given a resource titled "Foundry note"
  When I rename it to "Foundry capacity note"
  Then the resource is titled "Foundry capacity note"
  And its updated_at is newer than before

@unit
Scenario: A resource title cannot be blank
  Given a resource titled "Foundry note"
  When I try to rename it to an empty title
  Then I see a validation error
  And the title is unchanged
```

### Enable / disable

```gherkin
@unit
Scenario: Disabling a resource removes it from generation scope
  Given an enabled resource "Shipments 2025"
  When I disable it
  Then it is marked disabled
  And it is excluded from the assembled generation context

@unit
Scenario: Enabling a resource returns it to scope
  Given a disabled resource "Shipments 2025"
  When I enable it
  Then it is marked enabled
  And it is included in the assembled generation context

@ui
Scenario: The enabled toggle in the table reflects and changes scope
  Given the Resources view lists a resource with an enabled toggle
  When I flip the toggle off
  Then the resource shows as disabled in the table
```

### Re-extract

```gherkin
@unit
Scenario: Re-extracting a file resource regenerates its extracted text
  Given a file resource whose extracted text was previously produced
  When I re-extract it
  Then extraction runs again against the stored original
  And the extracted text is refreshed
  And its token_estimate is recomputed

@unit
Scenario: Re-extracting recovers a previously failed extraction
  Given a file resource with extraction status "failed"
  When I re-extract it with a working extractor
  Then its status becomes "extracted"
  And its extracted text is populated

@unit
Scenario: Re-extract is unavailable for a text-paste resource
  Given a text-paste resource
  Then no "re-extract" action is offered
```

### Preview

```gherkin
@unit
Scenario: Previewing shows the current extracted text
  Given a resource with extracted text "Wafer starts rose sharply."
  When I preview it
  Then I see "Wafer starts rose sharply."

@ui
Scenario: Selecting a resource shows its preview and metadata
  Given the Resources view lists resources
  When I select one
  Then the preview pane shows its extracted text, type, byte size, and token estimate
```

### Token-estimate contribution

```gherkin
@unit
Scenario: Each resource shows its own token-estimate contribution
  Given resources with token estimates 100, 250, and 400
  When I view the resources table
  Then each row shows its token estimate

@unit
Scenario: The resources view shows the total estimate for enabled resources
  Given enabled resources estimated at 100 and 250 and a disabled one at 400
  When I view the resources total
  Then the enabled-scope total is 350
  And the disabled resource is excluded from the total
```

### Remove

```gherkin
@unit @integration
Scenario: Removing a resource deletes its row and files
  Given a resource with an original blob and extracted text on disk
  When I remove it and confirm
  Then the resource no longer exists
  And its "projects/{projectId}/resources/{resourceId}" directory is deleted

@ui
Scenario: Removing a resource asks for confirmation
  Given a resource is selected
  When I choose Remove
  Then I am asked to confirm before anything is deleted
```
