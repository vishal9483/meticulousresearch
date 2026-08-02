# Tests — First-Run Onboarding

**SPEC:** §3.8 (first-run onboarding). **Milestone:** M5.
**Depends on:** settings-secure-key, projects-crud

## Traceability
- §3.8(1) welcome + privacy statement (local-first; where data lives) → Welcome scenarios.
- §3.8(2) API key entry + "Test key" verifies connectivity and lists models → Key-step scenarios (network mocked).
- §7.5 key resolution: when `ANTHROPIC_API_KEY` is supplied by the environment the key step is already satisfied → Env-provided key scenario.
- §3.8(3) defaults: model tier, theme, data directory (sensible defaults pre-filled) → Defaults scenarios.
- §3.8(4) optional sample project (a couple of resources + an example Market Research Report artifact) → Sample-project scenarios.
- §3.8(5) done → Projects home with contextual hints → Finish scenarios.
- §3.8 skippable and re-runnable from Settings → Skip + Re-run scenarios.
- §9.1(1) branded first-run onboarding → whole feature; §9.1(2) enter and validate an API key → Key-step.

---

```gherkin
Feature: First-run onboarding
  As a new analyst on a clean machine
  I want a guided first run that gets me to a working state
  So that the app is usable immediately and I understand where my data lives
```

### First-run trigger

```gherkin
@unit
Scenario: Onboarding runs on first launch
  Given a fresh installation with no completed onboarding
  When the app launches
  Then onboarding is shown starting at the Welcome step

@unit
Scenario: Onboarding does not run again after completion
  Given onboarding has been completed
  When the app launches
  Then onboarding is not shown
  And the app opens on the Projects home
```

### Welcome & privacy

```gherkin
@ui
Scenario: The Welcome step states the privacy posture and data location
  Given onboarding is on the Welcome step
  Then I see a brief product intro
  And a privacy statement that data is local-first
  And where the data directory lives
```

### API key step

```gherkin
@unit @requires-key
Scenario: A valid key can be tested and stored during onboarding
  Given onboarding is on the API key step
  And a mocked API that returns a model list for a valid key
  When I enter a key and click "Test key"
  Then I see a success confirmation with the available models
  And on continuing, the key is saved via the secure key store

@unit
Scenario: An invalid key shows an actionable error and blocks continue
  Given onboarding is on the API key step
  And a mocked API that returns 401 Unauthorized
  When I enter a key and click "Test key"
  Then I see a human-readable "key is invalid" error
  And no raw stack trace is shown
  And I cannot advance until a key is validated or I skip

@unit
Scenario: The key entered in onboarding is stored securely, not in plaintext
  Given I completed the API key step with a valid key
  Then the key is retrievable via the secure key store
  And the key string does not appear in db.sqlite or any settings file

@unit
Scenario: The key step is pre-satisfied when the key comes from the environment
  Given onboarding is on the API key step
  And the environment variable "ANTHROPIC_API_KEY" is set to "sk-from-env"
  Then the step indicates a key is already provided by the environment
  And I can continue without entering or storing a key
  And "sk-from-env" is not written to the secure key store or any settings file
```

### Defaults step

```gherkin
@unit
Scenario: The defaults step pre-fills sensible defaults
  Given onboarding is on the Defaults step
  Then the default model tier is pre-filled to Claude Opus 5
  And the theme is pre-filled to "System"
  And the data directory is pre-filled to the default location

@unit
Scenario Outline: Choosing defaults persists them to settings
  Given onboarding is on the Defaults step
  When I set "<setting>" to "<value>" and continue
  Then "<setting>" is saved to settings as "<value>"

  Examples:
    | setting        | value        |
    | default model  | claude-sonnet-5 |
    | theme          | Dark         |
    | data directory | a writable chosen path |
```

### Sample project (optional)

```gherkin
@unit
Scenario: Opting in creates a populated sample research project
  Given onboarding is on the Sample project step
  When I choose to create the sample project
  Then a sample project exists
  And it contains a couple of resources
  And it contains an example "Market Research Report" artifact

@unit
Scenario: Declining the sample project creates nothing
  Given onboarding is on the Sample project step
  When I decline the sample project
  Then no sample project is created

@unit
Scenario: The sample project is skipped without error when no key is configured
  Given I skipped the API key step
  When I opt into the sample project
  Then the sample project is created from bundled content without a network call
```

### Finish, skip & re-run

```gherkin
@unit
Scenario: Completing onboarding marks it done and lands on Projects home
  Given I am on the final step
  When I finish onboarding
  Then onboarding is marked complete
  And the app shows the Projects home
  And contextual hints on the primary actions are shown

@unit
Scenario: Onboarding is skippable at any step
  Given onboarding is on any step
  When I choose "Skip"
  Then onboarding is marked complete
  And the app shows the Projects home

@ui
Scenario: Onboarding can be re-run from Settings
  Given onboarding has been completed
  When I choose "Re-run onboarding" in Settings
  Then onboarding is shown again starting at the Welcome step

@manual
Scenario: Onboarding reads as branded, finished software
  Given the first-run onboarding flow
  Then each step is branded, styled, and free of placeholder or unstyled chrome
```
