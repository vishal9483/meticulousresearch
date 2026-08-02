# Tests — Image Vision Caption

**SPEC:** §3.2.1 (image resources via Claude native vision; optional caption cache). **Milestone:** M1.
**Depends on:** file-upload-extraction, ai-gateway (M2 — see note)

> **Dependency note:** the optional caption cache makes a small vision call through
> `IChatService`, which is owned by **ai-gateway (M2)**. Adding/storing/previewing an image
> resource does **not** require the network; only caption generation does. Caption scenarios use
> a **mocked `IChatService`** (per TESTING-STRATEGY §4) and stay `@unit`.

## Traceability
- §3.2.1 no OCR/vision library; images sent as image content blocks → No-OCR & content-block scenarios.
- §3.2.1 storage (original image in project files dir; base64/inline assembled at request time) → Storage scenarios.
- §3.2.1 optional caption cache (small vision call → stored as extracted text; searchable/previewable) → Caption scenarios.
- §3.2.1 / §6.3 model must accept image input; warn/switch if not → Vision-capability scenario.
- §3.2 per-resource fields (byte size, token estimate; caption as "extracted text") → Fields scenario.
- §9.1(3) add an image resource and see it previewed/token-estimated → covered here.

---

```gherkin
Feature: Image resources via vision
  As an analyst
  I want to add charts, scans, and screenshots as resources
  So that Claude reads them with native vision, without any OCR library
```

```gherkin
Background:
  Given a project "Semiconductors 2026" is open
  And the AI gateway is a mocked IChatService
```

### Adding an image

```gherkin
@unit @integration
Scenario Outline: Adding a supported image stores the original in the project
  Given the "Add resource" menu is open
  When I add an image "<name>" of type "<ext>"
  Then a resource "<name>" of type "image" exists
  And its original is stored under "projects/{projectId}/resources/{resourceId}/original.<ext>"
  And its byte_size equals the image file size

  Examples:
    | ext  | name            |
    | png  | Revenue chart   |
    | jpg  | Filing scan     |
    | jpeg | Booth photo     |
    | gif  | Trend animation |
    | webp | Dashboard shot  |

@unit
Scenario: An unsupported image type is rejected
  When I try to add an image of type "bmp"
  Then I see a message that the type is not supported
  And no resource is created

@unit
Scenario: No OCR or external vision library is used at add-time
  Given I add a PNG image resource
  Then no OCR/text-extraction library is invoked
  And any text understanding is deferred to the model at request time
```

### Sending to the model as vision

```gherkin
@unit
Scenario: An enabled image is assembled as an image content block at request time
  Given an enabled image resource "Revenue chart"
  When the request context is assembled for a generation
  Then the image is included as an image content block referencing the stored original
  And its bytes are inlined (base64) at that time, not stored inline in the DB

@unit
Scenario: Image tokens count toward the context budget
  Given an enabled image resource
  When the pre-send estimate is computed
  Then the image contributes an estimated token amount to the total
```

### Optional caption cache

```gherkin
@unit
Scenario: On add, a short caption is generated and stored as extracted text
  Given caption-on-add is enabled
  And the mocked IChatService returns the caption "Bar chart of 2025 foundry revenue by region"
  When I add a PNG image resource
  Then one small vision call is made
  And the resource's extracted text is "Bar chart of 2025 foundry revenue by region"

@unit
Scenario: The cached caption makes the image findable and previewable without resending it
  Given an image resource with cached caption "Bar chart of 2025 foundry revenue by region"
  When I preview the resource
  Then I see the caption text alongside a thumbnail
  And no vision call is made to display the preview

@unit
Scenario: Caption generation is optional and failure does not block adding the image
  Given caption-on-add is enabled
  And the vision call fails
  When I add an image resource
  Then the image resource is still created with its original stored
  And its extracted text is empty
  And I can trigger caption generation later

@unit
Scenario: With caption-on-add disabled, no vision call is made on add
  Given caption-on-add is disabled
  When I add an image resource
  Then no vision call is made
  And the resource is created with empty extracted text
```

### Vision-capable model requirement

```gherkin
@unit
Scenario: Selecting a non-vision model with image resources in scope warns the user
  Given an enabled image resource is in scope
  And the selected model does not accept image input
  When I attempt a generation
  Then I am warned and offered to switch to a vision-capable model
  And the generation does not silently drop the image
```

### UI

```gherkin
@ui
Scenario: An image resource shows a thumbnail and cached caption in the preview pane
  Given an image resource with a cached caption
  When I select it in the resources table
  Then the preview pane shows a thumbnail and the caption
```
