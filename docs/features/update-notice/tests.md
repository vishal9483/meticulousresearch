# Tests — Update Notice

**SPEC:** §8 (installer & updates — at minimum an in-app "update available" notice). **Milestone:** M6.
**Depends on:** installer

## Traceability
- §8 update mechanism (at minimum in-app "update available" notice) → Notice scenarios.
- §8 app version shown in About → Version-comparison scenarios (About display owns the string; here we compare it).
- §1.3 / §3.7 non-blocking, no dead ends → Non-blocking scenarios.
- §7.5 network egress only to Anthropic / clear offline behavior → Offline & failure scenarios.
- §9.1(10) no raw errors → Failure-handling scenario.

---

```gherkin
Feature: Update notice
  As an analyst
  I want to be told when a newer version is available
  So that I can update without hunting for it, and without being interrupted

Background:
  Given the app knows its current installed version
```

### Version comparison

```gherkin
@unit
Scenario Outline: Comparing the current version to the latest available
  Given the current version is "<current>"
  And the latest advertised version is "<latest>"
  When the app checks for updates
  Then an update is considered available: <available>

  Examples:
    | current | latest  | available |
    | 1.0.0   | 1.0.1   | yes       |
    | 1.0.0   | 1.1.0   | yes       |
    | 1.0.0   | 2.0.0   | yes       |
    | 1.0.1   | 1.0.1   | no        |
    | 1.2.0   | 1.1.9   | no        |

@unit
Scenario: Pre-release or malformed version strings do not trigger a false notice
  Given the current version is "1.0.0"
  And the latest advertised version is malformed or unreadable
  When the app checks for updates
  Then no update is considered available
  And no error is surfaced to the user
```

### In-app notice

```gherkin
@unit
Scenario: An available update produces a dismissible notice state
  Given the latest version is newer than the current version
  When the app checks for updates
  Then an "update available" notice state is raised
  And it includes the new version number
  And it is marked non-blocking and dismissible

@ui
Scenario: The update-available notice appears non-modally
  Given a newer version is available
  When I am using the app
  Then I see a non-modal "update available" notice (e.g. a banner or toast)
  And I can continue working without acting on it

@ui
Scenario: Dismissing the notice lets me keep working
  Given the "update available" notice is showing
  When I dismiss it
  Then the notice goes away
  And my current work is unaffected

@unit
Scenario: A dismissed notice is not shown again for the same version
  Given I dismissed the notice for version "1.0.1"
  When the app checks again and the latest is still "1.0.1"
  Then the notice is not raised again
  And it will be raised again if a newer version than "1.0.1" appears
```

### Non-blocking & offline behavior

```gherkin
@unit
Scenario: The update check never blocks app usage
  Given the update check is slow or pending
  When I use the app
  Then no UI is blocked waiting on the update check

@unit
Scenario: An update check failure is silent to the user
  Given the update check cannot reach the update source (offline or error)
  When the check runs
  Then no update notice is shown
  And no raw error or stack trace is surfaced
  And the app continues normally

@unit
Scenario: Being up to date shows no notice
  Given the current version equals the latest available version
  When the app checks for updates
  Then no notice is raised
```
