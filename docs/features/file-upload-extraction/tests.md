# Tests — File Upload & Extraction

**SPEC:** §3.2 (resources — file upload + extraction pipeline). **Milestone:** M1.
**Depends on:** text-paste-resource

## Traceability
- §3.2 types — *file upload* (PDF, DOCX, TXT, MD, CSV, XLSX) → Upload scenarios.
- §3.2 extraction pipeline (store original blob **and** extracted text; extract plain text + lightweight structure) → Extraction scenarios.
- §3.2 per-resource fields (source path, byte size, token estimate) → Fields scenario.
- §3.3/§3.7 extraction-failed error state (human-readable, recovery action) → Failure scenario.
- §9.1(3) add mixed resources (PDF, DOCX, XLSX) and see them extracted, previewed, token-estimated → covered here for file types.

---

```gherkin
Feature: File upload resources
  As an analyst
  I want to drop documents and datasets into a project
  So that their contents ground Claude's answers while the originals are preserved
```

```gherkin
Background:
  Given a project "Semiconductors 2026" is open
```

### Supported types & extraction

```gherkin
@unit @integration
Scenario Outline: Uploading a supported file extracts text and keeps the original
  Given the "Add resource" menu is open
  When I upload a "<ext>" file "<name>"
  Then a resource "<name>" of type "file" exists
  And its original blob is stored under "projects/{projectId}/resources/{resourceId}/original.<ext>"
  And its extracted text is stored under the resource's "extracted.txt"
  And the extracted text contains the document's readable content

  Examples:
    | ext  | name              |
    | pdf  | Foundry filing    |
    | docx | Analyst brief     |
    | txt  | Raw notes         |
    | md   | Methodology       |
    | csv  | Shipments 2025    |
    | xlsx | Forecast model    |

@unit
Scenario Outline: Tabular files extract row/column structure as text
  Given a "<ext>" file with columns "Segment, 2025, 2026" and two data rows
  When I upload it
  Then the extracted text preserves the header and both rows in a readable tabular form

  Examples:
    | ext  |
    | csv  |
    | xlsx |

@unit
Scenario: XLSX with multiple sheets extracts each sheet
  Given an "xlsx" file with sheets "Summary" and "Detail"
  When I upload it
  Then the extracted text includes content from both "Summary" and "Detail"
```

### Fields & sizing

```gherkin
@unit @integration
Scenario: An uploaded file records its source name, byte size, and token estimate
  Given a "pdf" file "Foundry filing.pdf" of 240 KB
  When I upload it
  Then the resource's source_uri references the original file name
  And its byte_size equals the uploaded file's size
  And its token_estimate is a positive number

@unit
Scenario: The original blob is copied into the project, not referenced in place
  Given a "docx" file located outside the project data directory
  When I upload it
  Then a copy is stored under the resource's directory
  And deleting the external source file does not affect the resource
```

### Validation & failures

```gherkin
@unit
Scenario: An unsupported file type is rejected
  Given the "Add resource" menu is open
  When I try to upload a ".pptx" file
  Then I see a message that the type is not supported
  And no resource is created

@unit
Scenario: A corrupt or unreadable document surfaces an extraction-failed state
  Given a "pdf" file whose contents cannot be parsed
  When I upload it
  Then the resource is created with its original blob stored
  And its extraction status is "failed" with a human-readable reason
  And I am offered a "re-extract" recovery action

@unit
Scenario: A scanned/image-only PDF with no text layer extracts empty text without crashing
  Given a "pdf" file that contains only scanned images
  When I upload it
  Then the resource is created with the original stored
  And the extracted text is empty
  And a hint suggests adding it as an image resource for vision
```

### UI

```gherkin
@ui
Scenario: Uploading shows async progress and then the extracted preview
  Given the Resources view is open
  When I upload a large "pdf" file
  Then I see a progress indicator while extraction runs
  And when it completes the preview pane shows the extracted text

@ui
Scenario: Dropping files onto the resources view uploads them
  Given the Resources view is open
  When I drag and drop a "docx" file onto it
  Then a file resource for that document is added
```
