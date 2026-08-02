# Tests — URL Resource

**SPEC:** §3.2 (resources — URL fetched & converted at add-time). **Milestone:** M1.
**Depends on:** text-paste-resource

## Traceability
- §3.2 types — *URL* (fetched and converted to text/markdown at add-time; original URL retained) → Add URL scenarios.
- §3.2 extraction pipeline (store extracted text; store fetched content) → Storage scenarios.
- §3.2 per-resource fields (source URL, byte size, token estimate) → Fields scenario.
- §3.5/§3.7 offline & error states (network required for generation; actionable errors) → Failure scenarios.
- §9.1(3) add a URL resource and see it extracted, previewed, token-estimated → covered here.

---

```gherkin
Feature: URL resources
  As an analyst
  I want to add a web page by its URL
  So that its readable content grounds Claude while the original link is kept for provenance
```

```gherkin
Background:
  Given a project "Semiconductors 2026" is open
  And URL fetching is served by a fake fetcher with scripted responses
```

### Adding a URL

```gherkin
@unit
Scenario: Adding a URL fetches and converts the page to text/markdown
  Given a page at "https://example.com/foundry" with an article body
  When I add the URL "https://example.com/foundry"
  Then a resource of type "url" exists
  And its source_uri is "https://example.com/foundry"
  And its extracted text is the readable content converted to markdown

@unit
Scenario: The original URL is retained as provenance
  Given I add the URL "https://example.com/report?id=42"
  When the resource is saved
  Then its source_uri is exactly "https://example.com/report?id=42"

@unit
Scenario: Page title becomes the default resource title
  Given a page whose title is "2025 Foundry Outlook"
  When I add its URL
  Then the resource title defaults to "2025 Foundry Outlook"

@unit
Scenario: Boilerplate is stripped from the converted content
  Given a page with navigation, ads, and an article body
  When I add its URL
  Then the extracted text contains the article body
  And it excludes the navigation and ad boilerplate
```

### Storage & fields

```gherkin
@unit @integration
Scenario: A URL resource stores extracted text and records size and token estimate
  Given a reachable page with a known body
  When I add its URL
  Then extracted text is written to the resource's "extracted.txt"
  And its byte_size and token_estimate are positive

@unit
Scenario: Content is converted at add-time, not re-fetched on preview
  Given a URL resource added while online
  When I later preview it while offline
  Then the previously converted text is shown without a network call
```

### Validation & failures

```gherkin
@unit
Scenario: A malformed URL is rejected
  Given the "Add resource" menu is open
  When I try to add "not-a-url"
  Then I see an inline validation error
  And no resource is created

@unit
Scenario Outline: Fetch failures surface an actionable error and create no resource
  Given the fetcher will respond with "<condition>"
  When I add the URL "https://example.com/x"
  Then I see a human-readable error for "<condition>"
  And no resource is created

  Examples:
    | condition       |
    | connection error|
    | HTTP 404        |
    | HTTP 500        |
    | timeout         |

@unit
Scenario: A page with no extractable text reports an empty-content error
  Given a page whose body has no readable text
  When I add its URL
  Then I see a message that no readable content was found
  And no resource is created
```

### UI

```gherkin
@ui
Scenario: Adding a URL shows fetch progress then the converted preview
  Given the Resources view is open
  When I add a URL
  Then I see a fetching indicator
  And on success the preview pane shows the converted markdown and the retained source URL
```
