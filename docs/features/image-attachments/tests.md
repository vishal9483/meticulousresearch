# Tests — Image Attachments (in-thread)

**SPEC:** §3.2.1 (multimodal messages — paste/attach an image into a turn, not only as a resource; image tokens count), §3.6 (image tokens count toward cost), §6.3 (vision flag). **Milestone:** M2.
**Depends on:** conversations, image-vision-caption

## Traceability
- §3.2.1 users can paste/attach an image directly into a conversation turn (not only as a persistent resource) → Attach scenarios.
- §3.3/§4 image attachments render as thumbnails inline in the thread → Thumbnail scenarios.
- §3.2.1/§3.6 image tokens count toward the context budget and cost → Token/cost scenarios.
- §3.2.1/§6.3 model must accept image input; warn/switch if selected model lacks vision → Vision-capability scenario.

> Distinct from image *resources* (`image-vision-caption`, M1): those persist as project
> knowledge; these are per-turn multimodal message content. Uses `FakeChatService`; no network.

---

```gherkin
Feature: In-thread image attachments
  As an analyst
  I want to paste or attach an image directly into a message
  So that I can ask about a chart or screenshot without saving it as a resource
```

### Attaching an image to a turn

```gherkin
@unit
Scenario: Pasting an image into the composer attaches it to the pending turn
  Given a conversation composer with the text "What does this chart show?"
  When I paste an image
  Then the pending turn carries the text and one image attachment

@unit
Scenario: Attaching an image file adds it to the pending turn
  Given a conversation composer
  When I attach an image file "chart.png"
  Then the pending turn carries an image attachment for "chart.png"

@unit
Scenario: A sent turn includes the image as a vision content block alongside the text
  Given a pending turn with text and one image attachment
  When I send the turn
  Then the request to the backend contains the user text
  And an image content block for the attachment

@unit
Scenario: An attached image is not created as a project resource
  Given a project with 0 resources
  When I send a turn with an image attachment
  Then the project still has 0 resources
  And the image is stored as message content, not as a resource

@unit
Scenario: Multiple images can be attached to a single turn
  Given a composer with two images attached
  When I send the turn
  Then the request contains two image content blocks

@unit
Scenario: An attachment can be removed before sending
  Given a composer with one image attached
  When I remove the attachment
  Then the pending turn has no image attachments
```

### Thumbnails inline (§4)

```gherkin
@ui
Scenario: Attached images render as inline thumbnails in the composer and the sent turn
  Given I attach an image in the composer
  Then a thumbnail is shown in the composer
  And after sending, the user turn shows the image as an inline thumbnail

@ui
Scenario: Clicking a thumbnail opens a larger preview
  Given a sent turn with an image thumbnail
  When I click the thumbnail
  Then a larger preview of the image is shown
```

### Tokens & cost (§3.2.1 / §3.6)

```gherkin
@unit
Scenario: Image tokens count toward the turn's input tokens and cost
  Given a turn with text and an image attachment
  And the backend reports input tokens that include the image's token cost
  When the turn completes
  Then the recorded input tokens include the image contribution
  And the per-turn cost reflects those input tokens

@unit
Scenario: The pre-send estimate includes an image token estimate
  Given a composer with one image attached
  When the pre-send token estimate is computed
  Then the estimate includes an estimated image token contribution
  And it is labeled "estimated"
```

### Vision capability (§3.2.1 / §6.3)

```gherkin
@unit
Scenario: Attaching an image while a non-vision model is selected warns and offers to switch
  Given the selected model has vision=false
  When I attach an image to the turn
  Then I see a warning that the model cannot read images
  And I am offered to switch to a vision-capable model
```
