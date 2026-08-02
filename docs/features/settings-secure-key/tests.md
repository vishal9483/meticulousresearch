# Tests — Settings & Secure API Key

**SPEC:** §3.5 (settings), §7.5 (security). **Milestone:** M0.
**Depends on:** data-store-migrations

## Traceability
- §7.5 key stored via Credential Manager/DPAPI, never plaintext → Secure storage scenarios.
- §7.5 key resolution `ANTHROPIC_API_KEY` env wins → store → none → Key resolution scenarios.
- §7.5/§3.5 endpoint resolution `ANTHROPIC_BASE_URL` env wins → setting → default public API → Base URL scenarios.
- §3.5 settings fields (base URL, default model, context budget, data dir, theme, telemetry off) → Settings scenarios.
- §3.8(2) "Test key" verifies connectivity **at the resolved base URL** → Test-key scenarios (network mocked).

---

```gherkin
Feature: Settings & secure API key storage
  As an analyst
  I want to store my API key securely and manage app defaults
  So that my credentials are safe and the app behaves the way I prefer
```

### Secure key storage

```gherkin
@unit @integration
Scenario: Saving an API key does not persist it in the database or plaintext
  Given a running app with an initialized data store
  When I save the API key "sk-ant-secret"
  Then the key is retrievable via the secure key store
  And the string "sk-ant-secret" does not appear in db.sqlite
  And the string "sk-ant-secret" does not appear in any settings file on disk

@unit
Scenario: Retrieving the key when none is set returns empty state
  Given no API key has been saved
  When the app reads the API key
  Then it reports that no key is configured

@unit @integration
Scenario: Overwriting the API key replaces the stored value
  Given an API key "sk-old" is stored
  When I save the API key "sk-new"
  Then retrieving the key returns "sk-new"

@unit @integration
Scenario: Clearing the API key removes it from secure storage
  Given an API key "sk-ant-secret" is stored
  When I clear the API key
  Then retrieving the key reports that no key is configured
```

### API key resolution (env wins)

```gherkin
@unit
Scenario: The ANTHROPIC_API_KEY environment variable takes precedence over the stored key
  Given the stored API key is "sk-stored"
  And the environment variable "ANTHROPIC_API_KEY" is set to "sk-from-env"
  When the app resolves the effective API key
  Then the effective key is "sk-from-env"

@unit
Scenario: The stored key is used when no environment variable is set
  Given the stored API key is "sk-stored"
  And the environment variable "ANTHROPIC_API_KEY" is not set
  When the app resolves the effective API key
  Then the effective key is "sk-stored"

@unit
Scenario: An empty environment variable does not override the stored key
  Given the stored API key is "sk-stored"
  And the environment variable "ANTHROPIC_API_KEY" is set to ""
  When the app resolves the effective API key
  Then the effective key is "sk-stored"

@unit
Scenario: No key anywhere reports that no key is configured
  Given no API key has been saved
  And the environment variable "ANTHROPIC_API_KEY" is not set
  When the app resolves the effective API key
  Then it reports that no key is configured

@unit @integration
Scenario: A key supplied via the environment is never written to storage
  Given no API key has been saved
  And the environment variable "ANTHROPIC_API_KEY" is set to "sk-from-env"
  When the app resolves the effective API key
  Then the string "sk-from-env" does not appear in db.sqlite
  And the string "sk-from-env" does not appear in any settings file on disk
  And the secure key store still reports that no key is configured
```

### API base URL / endpoint resolution (env wins)

```gherkin
@unit
Scenario: The base URL defaults to the public Anthropic API when nothing is configured
  Given no API base URL setting has been saved
  And the environment variable "ANTHROPIC_BASE_URL" is not set
  When the app resolves the effective base URL
  Then the effective base URL is the default public Anthropic API endpoint

@unit
Scenario: A persisted base URL setting overrides the default
  Given the API base URL setting is "https://llm.example.internal"
  And the environment variable "ANTHROPIC_BASE_URL" is not set
  When the app resolves the effective base URL
  Then the effective base URL is "https://llm.example.internal"

@unit
Scenario: The ANTHROPIC_BASE_URL environment variable takes precedence over the setting
  Given the API base URL setting is "https://llm.example.internal"
  And the environment variable "ANTHROPIC_BASE_URL" is set to "https://llm.sdc.siemens.cloud"
  When the app resolves the effective base URL
  Then the effective base URL is "https://llm.sdc.siemens.cloud"

@unit
Scenario: A base URL supplied via the environment is shown as an override and not persisted
  Given no API base URL setting has been saved
  And the environment variable "ANTHROPIC_BASE_URL" is set to "https://llm.sdc.siemens.cloud"
  When the Settings screen is opened
  Then the base URL field shows "https://llm.sdc.siemens.cloud" as environment-provided
  And the persisted base URL setting remains unset
```

### Test key

```gherkin
@unit @requires-key
Scenario: Testing a valid key reports success and lists models
  Given a stored API key and a mocked API that returns a model list
  When I click "Test key"
  Then I see a success confirmation
  And the available models are listed

@unit
Scenario: Test key calls the resolved base URL, not a hardcoded endpoint
  Given the effective base URL is "https://llm.sdc.siemens.cloud"
  And a mocked API at that base URL that returns a model list
  When I click "Test key"
  Then the request is sent to "https://llm.sdc.siemens.cloud"
  And I see a success confirmation

@unit
Scenario: Testing an invalid key reports a clear, actionable error
  Given a stored API key and a mocked API that returns 401 Unauthorized
  When I click "Test key"
  Then I see a human-readable error indicating the key is invalid
  And no raw stack trace is shown
```

### App settings

```gherkin
@unit @integration
Scenario Outline: Settings persist across restart
  Given I set "<setting>" to "<value>"
  When the app restarts
  Then "<setting>" is still "<value>"

  Examples:
    | setting        | value                          |
    | default model  | claude-opus-5                  |
    | theme          | dark                           |
    | context budget | 150000                         |
    | telemetry      | off                            |
    | api base url   | https://llm.example.internal   |

@unit
Scenario: Telemetry is off by default
  Given a fresh installation
  When settings are first read
  Then telemetry is off

@unit
Scenario: Default model defaults to Claude Opus 5
  Given a fresh installation
  When settings are first read
  Then the default model is "claude-opus-5"

@ui
Scenario: Changing the data directory is validated before saving
  Given the Settings screen is open
  When I set the data directory to a path that is not writable
  Then I see an inline validation error
  And the change is not saved
```
