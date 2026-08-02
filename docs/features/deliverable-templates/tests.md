# Tests — Deliverable Templates

**SPEC:** §3.4.1 (deliverable templates), §6.3 (config-driven catalog philosophy). **Milestone:** M3.
**Depends on:** artifact-creation

## Traceability
- §3.4.1 config-driven JSON/Markdown template library → Catalog scenarios.
- §3.4.1 bundled table (8 templates) → Bundled-templates scenario.
- §3.4.1 each template declares id/name/description/target type/section scaffold/generation prompt (placeholders scope+horizon+region)/default model tier → Template-fields scenarios.
- §3.4.1 grounding-first prompting (cite in-scope resources, flag unsupported claims) → Grounding scenarios.
- §3.4.1 surfaced in New artifact + New project flows with previews → Gallery scenarios.
- §9.1(2) create a project from a deliverable template → New-project-from-template scenarios.
- §9.1(5) generate a Market Research Report from a template → Flagship scenario.

---

```gherkin
Feature: Deliverable templates
  As an analyst
  I want research-grade templates that steer Claude to firm-quality, grounded output
  So that I can produce a professional deliverable out of the box
```

Background (per TESTING-STRATEGY §4): templates load from a config JSON (a default that ships
with the app, overridable in Settings, like the model catalog §6.3); AI generation is served by
a deterministic `FakeChatService`.

```gherkin
Background:
  Given the default deliverable-template catalog is loaded
  And a project "Grid Storage 2026" with 2 enabled resources
  And AI generation is served by a deterministic FakeChatService
```

### Config-driven catalog

```gherkin
@unit
Scenario: The template catalog loads from config JSON
  When the template library is loaded
  Then the templates come from the config file, not hard-coded values

@unit
Scenario: A user-provided template is added without a rebuild
  Given a Settings override that adds a template "House Brief"
  When the template library is loaded
  Then "House Brief" appears in the gallery alongside the bundled templates

@unit
Scenario: A malformed template config surfaces a clear error, not a crash
  Given a template config missing a required "id" on one entry
  When the template library is loaded
  Then loading reports a descriptive validation error identifying the bad entry
  And the valid entries still load
```

### Bundled templates (§3.4.1 table)

```gherkin
@unit
Scenario: All eight bundled templates are present with their target types
  When the default catalog is loaded
  Then it contains these templates and target artifact types:
    | template                     | target type |
    | Market Research Report       | doc         |
    | Executive Summary / Brief    | doc         |
    | Competitive Landscape        | table       |
    | Market Forecast Model        | table       |
    | SWOT / Porter's Five Forces  | doc         |
    | Company / Vendor Profile     | doc         |
    | Customer / Buyer Insights    | doc         |
    | Trend / Technology Scan      | doc         |
```

### Template fields (§3.4.1)

```gherkin
@unit
Scenario: A template declares all required fields
  Given the "Market Research Report" template
  Then it declares an id, display name, description, target artifact type, section scaffold, a generation prompt, and a default model tier

@unit
Scenario: The Market Research Report scaffold has the specified sections
  Given the "Market Research Report" template
  Then its section scaffold includes "Executive summary", "Market sizing & 10-yr forecast", "Competitive landscape", "Regional analysis", and "Methodology & sources"

@unit
Scenario Outline: A template recommends a default model tier
  Given the "<template>" template
  Then its default model tier is "<tier>"

  Examples:
    | template               | tier     |
    | Market Research Report | Deep     |
    | Executive Summary / Brief | Balanced |
```

### Prompt placeholders — scope / horizon / region

```gherkin
@unit
Scenario: Placeholders are substituted into the generation prompt
  Given the "Market Research Report" template whose prompt contains {scope}, {horizon}, and {region}
  When I supply scope "Grid-scale battery storage", horizon "2026–2036", and region "North America"
  Then the assembled prompt contains "Grid-scale battery storage", "2026–2036", and "North America"
  And no unresolved "{scope}", "{horizon}", or "{region}" placeholder remains

@unit
Scenario: An unfilled optional placeholder falls back to a sensible default
  Given a template with a {region} placeholder
  When I leave region blank
  Then the assembled prompt uses "Global" for region
```

### Grounding-first prompting (§3.4.1)

```gherkin
@unit
Scenario: The assembled prompt instructs the model to cite in-scope resources
  Given any bundled template
  When I assemble its generation prompt with 2 in-scope resources
  Then the prompt instructs the model to cite which in-scope resource supports each claim

@unit
Scenario: The assembled prompt instructs the model to flag unsupported claims
  Given any bundled template
  When I assemble its generation prompt
  Then the prompt instructs the model to flag assertions not supported by the in-scope resources

@unit
Scenario: Only enabled resources are passed as in-scope for grounding
  Given a project with 3 resources, one disabled
  When I generate from a template
  Then the version's resource_scope_json contains only the 2 enabled resource ids
```

### Generate from a template (produces an artifact)

```gherkin
@unit
Scenario: Generating from a template creates an artifact of the template's target type
  Given the "Competitive Landscape" template (target type "table")
  When I generate from it with scope "EV charging networks"
  Then an artifact of type "table" is created
  And its first version records the template's id, the assembled prompt, the model, and the in-scope resources

@unit
Scenario: The generated artifact follows the template's section scaffold
  Given the "Market Research Report" template
  And a FakeChatService scripted to echo the scaffold headings
  When I generate from it
  Then the artifact content contains the scaffold's section headings in order
```

### Flagship — Market Research Report (§9.1(5))

```gherkin
@unit
Scenario: Generate a Market Research Report artifact from the flagship template
  Given the "Market Research Report" template
  When I generate from it with scope "Grid-scale storage", horizon "2026–2036", region "North America"
  Then a "doc" artifact titled from the template is created
  And it is grounded in the project's enabled resources
  And its version records model, prompt, in-scope resources, and usage
```

### Gallery in New-artifact and New-project flows

```gherkin
@ui
Scenario: The New-artifact flow surfaces the template gallery with previews
  Given the "New artifact" flow is open
  Then the template gallery is shown with each template's name, description, and a preview

@ui
Scenario: The New-project flow surfaces the template gallery
  Given the Projects home is open
  When I choose "New project"
  Then the template gallery is shown so I can start a project from a template

@unit
Scenario: Creating a project from a template seeds a first artifact from that template
  Given the "Market Research Report" template
  When I create a project "Storage Study" from it
  Then a project "Storage Study" exists
  And it contains a Market Research Report artifact generated from the template
```
