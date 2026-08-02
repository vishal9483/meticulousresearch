# Phase — Conversations

**SPEC:** §3.3, §5, §7.3. **Milestone:** M2. **Depends on:** ai-gateway, projects-crud

## Goal
Project-scoped Q&A threads grounded in the project's resources. Owns the **conversation/message
domain + service**, **grounding assembly** (custom instructions + enabled resources + history +
message, §7.3), and **message persistence** (§5). Drives generation via `IChatService`
(`ai-gateway`); does not own streaming UI, model selection, or per-turn actions.

## Deliverables
1. **`IConversationService`** in Core: `Create(projectId)`, `List(projectId)`, `Get`, `Delete`,
   `Ask(conversationId, message, model, resourceScope, cancellationToken)`, `GetMessages`.
2. **Domain model** matching §5 `Conversation` (project_id, title, model_default, timestamps) and
   `Message` (role, content, model, tokens_in/out, tokens_cache_read/write, cost_usd, latency_ms,
   resource_scope_json, created_at).
3. **Grounding assembler** — builds the request payload: project custom instructions (system) +
   *enabled* in-scope resources' extracted text + ordered conversation history + user message.
   This is the single place §7.3 assembly happens (shared with artifact generation later).
4. **Ask flow** — persist the user `Message`, invoke `IChatService.Ask`, and on completion
   persist the assistant `Message` with model, tokens, latency, and resource_scope; advance the
   conversation `updated_at`; auto-title a new conversation from the first turn.
5. **Conversation thread view/VM** — thread of user/assistant turns, composer, designed empty
   state. Streaming rendering is added by `streaming`; the VM exposes the hooks it needs.

## Suggested design
- Grounding assembler takes the project, conversation history, resource scope, and message and
  returns the structured request `IChatService` consumes; keep it pure for `@unit` tests.
- Resource scope defaults to the project's currently enabled resources but is passed explicitly
  so `context-budget` (M1) and the scope panel can adjust it.
- Persist tokens as ground truth (§3.6); `cost_usd` snapshot is filled by `cost-tracking` later —
  leave the column and compute-hook available.
- Latency measured with injected `IClock`; auto-title can be a truncation of the first user
  message initially (a smarter title is out of scope for M2).
- Use `FakeChatService` for all `@unit` tests; no network.

## Test-first order
1. Scope `@unit` tests (belongs-to-one-project, listing isolation, cascade delete) → domain +
   service skeleton.
2. Grounding `@unit` tests (instructions/resources/history/message, disabled excluded, order,
   recorded scope) → grounding assembler.
3. Ask-flow/persistence `@unit` tests (user then assistant, usage/model/latency, updated_at,
   auto-title) → `Ask` + persistence.
4. `@ui` tests (thread shows both turns, empty state) → thread view.

## Definition of done
- Every conversation belongs to exactly one project; listing/deletion are project-isolated.
- Grounding includes instructions + enabled resources + ordered history + message; disabled
  resources excluded; scope recorded on the turn.
- Ask persists user + assistant messages with model, tokens, latency, and resource_scope; the
  conversation's updated_at advances and a new conversation gets a title.

## Notes for later features
- `model-selector` supplies/records the model per turn (per-conversation + per-message override).
- `streaming` renders tokens live, adds stop/cancel, and persists interrupted turns.
- `turn-metadata-actions` adds copy/retry/edit-resend/promote/delete and the per-turn cost badge.
- `image-attachments` extends a turn to carry inline image content blocks alongside text.
- `prompt-caching` marks the assembled system prompt + stable resources with cache breakpoints.
