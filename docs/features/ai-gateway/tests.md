# Tests — AI Gateway (IChatService / IArtifactService)

**SPEC:** §7.2 (sidecar), §7.3 (request flow), §7.1 (layers), §8 (sidecar auto-restart). **Milestone:** M2.
**Depends on:** settings-secure-key

## Traceability
- §7.2 Agent SDK sidecar is the **primary** path over loopback WebSocket → Sidecar transport scenarios.
- §7.2/§7.5 API key passed over a secure channel (never on command line), per-session token → Secure key/token scenarios.
- §7.5 effective key & base URL resolved via env-wins order; endpoint never hardcoded → Key & endpoint resolution scenarios.
- §7.2 C# direct-API is the **fallback**; both implement the same contract; selectable in Settings → Backend selection scenarios.
- §7.3 assemble system prompt + resources + history, stream tokens, persist usage on completion → Request flow scenarios.
- §3.6 both backends surface API `usage` fields (in/out, cache read/write) → Usage capture scenarios.
- §8 crashed sidecar auto-restarts → Auto-restart scenarios.
- §9.1(4) streaming conversation (contract underlies it; streaming/model live in their own features).

> This feature **owns the `IChatService` / `IArtifactService` contracts** consumed by
> conversations, streaming, model-selector, tools, backoff, caching, and artifacts. Tests here
> pin the contract and the two backends behind it. All scenarios are deterministic; only
> `@requires-network` touches the real API.

---

```gherkin
Feature: AI gateway
  As the app
  I want a single generation contract with a primary sidecar and a direct-API fallback
  So that the rest of the app is unaware which backend produces answers
```

### The IChatService contract (backend-agnostic)

```gherkin
@unit
Scenario: A chat request yields a stream of tokens then a completion with usage
  Given a chat backend scripted to emit "Hello", " ", "world" then complete
  When I ask a question through IChatService
  Then I receive the tokens "Hello", " ", "world" in order
  And a completion carrying the final text "Hello world"
  And usage fields for input and output tokens

@unit
Scenario: The request is assembled from custom instructions, resources, history, and message
  Given a project with custom instructions "Cite sources"
  And two enabled resources in scope
  And a prior turn in the conversation history
  When I ask "What is the market size?" through IChatService
  Then the request the backend receives contains the custom instructions as system context
  And the two in-scope resources
  And the prior turn
  And the user message "What is the market size?"

@unit
Scenario: The selected model is forwarded to the backend
  Given a chat backend that records the requested model
  When I ask a question with model "claude-sonnet-5"
  Then the backend request specifies model "claude-sonnet-5"

@unit
Scenario: Cancelling a request stops the stream promptly
  Given a chat backend mid-stream
  When I cancel the request
  Then no further tokens are delivered
  And the request completes in a cancelled state
```

### Usage capture (both backends)

```gherkin
@unit
Scenario Outline: Usage fields are surfaced identically regardless of backend
  Given the "<backend>" backend scripted to report usage in=1200 out=350 cache_read=800 cache_write=200
  When a request completes
  Then the reported usage is input=1200, output=350, cache_read=800, cache_write=200

  Examples:
    | backend    |
    | sidecar    |
    | direct-api |

@unit
Scenario: Missing cache fields default to zero, not error
  Given a backend that reports only input and output tokens
  When a request completes
  Then cache_read and cache_write are reported as 0
```

### Backend selection (Settings)

```gherkin
@unit
Scenario: The sidecar is the default backend
  Given a fresh installation
  When the gateway resolves its backend
  Then the Agent SDK sidecar backend is selected

@unit
Scenario Outline: Settings selects which backend is active
  Given the backend preference is set to "<choice>"
  When the gateway resolves its backend
  Then the active backend is "<active>"

  Examples:
    | choice     | active     |
    | sidecar    | sidecar    |
    | direct-api | direct-api |

@unit
Scenario: The rest of the app cannot tell which backend answered
  Given identical scripted output for both backends
  When I ask the same question via each backend
  Then the tokens, completion text, and usage are equivalent
```

### Sidecar transport & security

```gherkin
@unit @integration
Scenario: The sidecar listens on loopback with an ephemeral port
  Given the sidecar is launched
  When it reports its endpoint
  Then the host is a loopback address
  And the port was assigned at launch

@unit @integration
Scenario: The gateway authenticates to the sidecar with a per-session token
  Given a launched sidecar with a per-session auth token
  When a client connects without the token
  Then the connection is refused
  And a client presenting the correct token is accepted

@unit @integration
Scenario: The API key is passed over the secure channel, never on the command line
  Given the sidecar is launched
  Then the launch command line does not contain the API key
  And the key is delivered to the sidecar over the authenticated channel
  And the key is obtained from the secure key store, not settings or plaintext

@unit
Scenario: Missing API key produces a clear, actionable error before any request
  Given no API key is configured
  And the environment variable "ANTHROPIC_API_KEY" is not set
  When I ask a question
  Then I see a human-readable "no API key" error with a link to Settings
  And no raw stack trace
```

### Key & endpoint resolution (env wins, endpoint never hardcoded)

```gherkin
@unit
Scenario: The gateway uses the API key from the environment when present
  Given the environment variable "ANTHROPIC_API_KEY" is set to "sk-from-env"
  And a different key "sk-stored" is in the secure key store
  When I ask a question
  Then the backend receives the key "sk-from-env"

@unit
Scenario: The gateway falls back to the stored key when the environment has none
  Given the environment variable "ANTHROPIC_API_KEY" is not set
  And the key "sk-stored" is in the secure key store
  When I ask a question
  Then the backend receives the key "sk-stored"

@unit @integration
Scenario: The environment key is delivered over the secure channel, never on the command line
  Given the environment variable "ANTHROPIC_API_KEY" is set to "sk-from-env"
  When the sidecar is launched and a request is made
  Then the launch command line does not contain "sk-from-env"
  And the key is delivered to the sidecar over the authenticated channel

@unit
Scenario Outline: Both backends target the resolved base URL, never a hardcoded endpoint
  Given the effective base URL is "https://llm.sdc.siemens.cloud"
  And the "<backend>" backend
  When I ask a question
  Then the request is sent to base URL "https://llm.sdc.siemens.cloud"

  Examples:
    | backend    |
    | sidecar    |
    | direct-api |

@unit
Scenario: The base URL from the environment overrides the persisted setting
  Given the persisted API base URL setting is "https://llm.example.internal"
  And the environment variable "ANTHROPIC_BASE_URL" is set to "https://llm.sdc.siemens.cloud"
  When the gateway resolves its endpoint
  Then requests are sent to base URL "https://llm.sdc.siemens.cloud"

@unit
Scenario: With no endpoint configured the gateway uses the default public Anthropic API
  Given no API base URL setting has been saved
  And the environment variable "ANTHROPIC_BASE_URL" is not set
  When the gateway resolves its endpoint
  Then requests are sent to the default public Anthropic API endpoint
```

### Sidecar auto-restart (§8)

```gherkin
@unit @integration
Scenario: A crashed sidecar is automatically restarted
  Given a running sidecar
  When the sidecar process exits unexpectedly
  Then the gateway restarts it
  And a new per-session token and endpoint are established

@unit
Scenario: An in-flight request during a sidecar crash surfaces a retryable error, not a lost turn
  Given a request is in flight
  When the sidecar crashes mid-stream
  Then the request fails with a retryable error
  And the partial work is preserved for the caller to resume
  # (resumable persistence detail is covered by streaming; here we only assert the signal)

@unit
Scenario: Repeated immediate crashes back off and report an unavailable backend
  Given the sidecar crashes on every launch
  When the gateway attempts to use it several times
  Then restart attempts are throttled
  And the user sees a clear "generation backend unavailable" error with a recovery hint
```

### Direct-API fallback

```gherkin
@unit
Scenario: The direct-API backend implements the same contract end to end
  Given the direct-api backend scripted to stream tokens and report usage
  When I ask a question
  Then I receive streamed tokens, a completion, and usage fields
  And no sidecar process is launched

@requires-network @requires-key
Scenario: The direct-API backend performs a real round trip (acceptance only)
  Given a valid API key and the direct-api backend
  When I ask "Reply with the single word: ok"
  Then I receive a streamed response
  And usage fields with non-zero input and output tokens
```
