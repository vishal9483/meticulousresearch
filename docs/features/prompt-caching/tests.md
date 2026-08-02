# Tests — Prompt Caching

**SPEC:** §8 (system prompt + stable resource context sent with cache breakpoints; cache-read/write metered), §3.6 (cache tokens included in cost). **Milestone:** M2.
**Depends on:** ai-gateway

## Traceability
- §8 system prompt (custom instructions) sent with a cache breakpoint → System-prompt cache scenarios.
- §8 stable resource context sent with cache breakpoints → Resource cache scenarios.
- §8 repeated turns/regenerations reuse cached input (cutting latency/cost) → Reuse scenarios.
- §8/§3.6 cache-read/write tokens are metered → Metering scenarios.
- §3.6 cache tokens included in per-turn cost → Cost scenarios.

> Uses `FakeChatService` that echoes the cache breakpoints it received and reports scripted
> cache_read/cache_write usage; the injected `IClock` keeps anything time-based deterministic.
> No network.

---

```gherkin
Feature: Prompt caching
  As an analyst
  I want stable context to be cached across turns
  So that repeated questions and regenerations are cheaper and faster
```

### Cache breakpoints on stable context (§8)

```gherkin
@unit
Scenario: The system prompt (custom instructions) is sent with a cache breakpoint
  Given a project with custom instructions
  When a request is assembled for a turn
  Then the system prompt segment carries a cache breakpoint

@unit
Scenario: Stable enabled resource context is sent with a cache breakpoint
  Given a conversation with two stable enabled resources in scope
  When a request is assembled
  Then the resource context segment carries a cache breakpoint

@unit
Scenario: The volatile tail (history + new message) is not marked cacheable
  Given a request with cached system + resources
  When it is assembled
  Then the new user message and recent history are outside the cache breakpoints
```

### Reuse across turns / regenerations (§8)

```gherkin
@unit
Scenario: A second turn with unchanged instructions and resources reuses the cache
  Given a first turn established the cache for the system prompt and resources
  When I ask a second question with the same instructions and resource scope
  Then the second request presents the same cache breakpoints
  And the backend reports cache-read tokens on the second turn

@unit
Scenario: Changing the enabled resource scope invalidates the resource cache segment
  Given a cached resource segment for resources {A, B}
  When I change the scope to {A, C} and ask again
  Then the resource cache segment reflects the new scope
  And is not served from the stale cache

@unit
Scenario: A regeneration (retry) of the same turn reuses the cached context
  Given a completed turn with cached system + resources
  When I retry the turn
  Then the retry request presents the same cache breakpoints
  And cache-read tokens are reported
```

### Metering & cost (§8 / §3.6)

```gherkin
@unit
Scenario: Cache-read and cache-write tokens are recorded on the turn
  Given a backend that reports cache_write=1500 on the first turn and cache_read=1500 on the next
  When each turn completes
  Then the first turn records tokens_cache_write 1500
  And the second turn records tokens_cache_read 1500

@unit
Scenario: Cache tokens are included in the per-turn cost using catalog cache rates
  Given catalog cache-read and cache-write rates for the model
  And a turn reporting cache_read and cache_write tokens
  When the per-turn cost is computed
  Then the cost includes the cache-read and cache-write contributions

@unit
Scenario: Missing cache usage records as zero, not error
  Given a backend that reports no cache fields
  When a turn completes
  Then tokens_cache_read and tokens_cache_write are 0
  And the cost has no cache contribution

@ui
Scenario: The per-turn cost breakdown itemizes cache read/write
  Given a turn that used prompt caching
  When I expand the cost breakdown
  Then cache-read and cache-write are shown as line items
```
