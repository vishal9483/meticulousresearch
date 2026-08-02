# Tests — Conversations

**SPEC:** §3.3 (conversations & Q&A), §5 (Conversation/Message), §7.3 (grounding/request flow). **Milestone:** M2.
**Depends on:** ai-gateway, projects-crud

## Traceability
- §3.3 a conversation belongs to exactly one project → Scope scenarios.
- §7.3 grounding = custom instructions + enabled resources + history + message → Grounding scenarios.
- §5 persists `Message` rows (role, content, model, tokens, latency, resource_scope, timestamps) → Persistence scenarios.
- §3.3 turn metadata recorded per assistant turn (detailed actions live in turn-metadata-actions) → Persistence scenarios.
- §9.1(4) hold a grounded, streaming conversation (streaming/model live in their own features) → Grounding + Ask flow scenarios.

## Traceability note
Token-by-token UI streaming (§3.3) is owned by `streaming`; model selection by `model-selector`;
per-turn actions/badges by `turn-metadata-actions`. This feature owns the conversation/message
domain, grounding assembly, and persistence, and drives generation via `IChatService` from
`ai-gateway` (mocked here as `FakeChatService`).

---

```gherkin
Feature: Conversations
  As an analyst
  I want project-scoped Q&A threads grounded in my resources
  So that every answer is tied to a project and its source material
```

### Scope

```gherkin
@unit
Scenario: A conversation belongs to exactly one project
  Given a project "Semiconductors 2026"
  When I create a conversation in it
  Then the conversation's project is "Semiconductors 2026"

@unit
Scenario: Conversations from other projects are not listed here
  Given a project "A" with 2 conversations
  And a project "B" with 1 conversation
  When I list conversations for project "A"
  Then I see exactly the 2 conversations from project "A"

@unit
Scenario: Deleting a conversation removes its messages
  Given a conversation with 4 messages
  When I delete the conversation
  Then the conversation no longer exists
  And its messages no longer exist
```

### Grounding (§7.3)

```gherkin
@unit
Scenario: The request is grounded in custom instructions, enabled resources, history, and the message
  Given a project with custom instructions "Formal tone; cite sources"
  And enabled resources "Filing.pdf" and "Interview.txt"
  And a conversation with one prior user/assistant turn
  When I ask "Summarize the competitive landscape"
  Then the grounded request includes the custom instructions as system context
  And the extracted text of "Filing.pdf" and "Interview.txt"
  And the prior turn
  And the new user message

@unit
Scenario: Disabled resources are excluded from grounding
  Given a project with an enabled resource "A" and a disabled resource "B"
  When I ask a question
  Then the grounded request includes "A"
  And does not include "B"

@unit
Scenario: The resource scope used is recorded on the assistant turn
  Given enabled resources "A" and "B" in scope
  When a turn completes
  Then the assistant message records resource_scope containing "A" and "B"

@unit
Scenario: History is sent in order
  Given a conversation with turns T1 then T2
  When I ask a third question
  Then the grounded request contains T1 before T2 before the new message
```

### Ask flow & persistence (§5)

```gherkin
@unit
Scenario: Asking a question persists a user message then an assistant message
  Given an empty conversation
  When I ask "What is the TAM?" and the backend completes with "The TAM is ..."
  Then a user message "What is the TAM?" is persisted
  And an assistant message "The TAM is ..." is persisted
  And the assistant message follows the user message

@unit
Scenario: The completed assistant turn persists usage, model, and latency
  Given a conversation using "claude-sonnet-5"
  And a backend that reports usage in=900 out=200 and completes after a measured interval
  When a turn completes
  Then the assistant message records model "claude-sonnet-5"
  And tokens_in 900 and tokens_out 200
  And a latency_ms value greater than 0

@unit
Scenario: A conversation's updated_at advances when a turn completes
  Given a conversation created at time T0
  When a turn completes at a later time
  Then the conversation's updated_at is newer than T0

@unit
Scenario: A new conversation gets a title
  Given an empty conversation with no title
  When the first turn completes
  Then the conversation has a non-empty title
```

### UI

```gherkin
@ui
Scenario: Sending a message shows both the user turn and the assistant reply in the thread
  Given a conversation is open
  When I type "Hello" and send
  Then my message appears in the thread
  And the assistant reply appears below it

@ui
Scenario: An empty conversation shows a designed empty state
  Given a new conversation with no messages
  When I open it
  Then I see an empty state prompting me to ask my first question
```
