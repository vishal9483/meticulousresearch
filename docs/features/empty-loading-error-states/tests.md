# Tests — Empty, Loading & Error States

**SPEC:** §3.7 (empty/loading/error states). **Milestone:** M5.
**Depends on:** design-system-theming

## Traceability
- §3.7 every list/view has a designed empty state with a clear call-to-action → Empty-state scenarios.
- §3.7 skeleton loaders during async work; no blank screens → Loading scenarios.
- §3.7 human-readable, actionable error states (missing key, offline, rate limited, extraction failed) with a recovery action, never a raw stack trace → Error-state scenarios.
- §9.1(10) no unstyled/placeholder screens and no raw errors → Consistency + no-stack-trace scenarios.

---

```gherkin
Feature: Empty, loading, and error states
  As an analyst
  I want every view to guide me when it is empty, working, or has failed
  So that I always know what to do next and never hit a dead end or raw error
```

### Empty states

```gherkin
@ui
Scenario Outline: Every primary list shows a designed empty state with a call-to-action
  Given the "<view>" has no items
  When I open it
  Then I see the designed empty-state message "<message>"
  And a call-to-action to "<cta>"
  And the screen is not blank

  Examples:
    | view          | message                                      | cta              |
    | Projects home | No projects yet                              | New project      |
    | Resources     | No resources yet                             | Add resource     |
    | Conversations | No conversations yet                         | New conversation |
    | Artifacts     | No artifacts yet                             | New artifact     |

@unit
Scenario: A list view-model exposes an empty state when its collection is empty
  Given a list view-model with zero items
  Then its state is "Empty"
  And it exposes a non-empty call-to-action command

@unit
Scenario: Adding the first item leaves the empty state
  Given a list view-model in the "Empty" state
  When an item is added
  Then its state is "Content"
```

### Loading states

```gherkin
@unit
Scenario: A view-model reports Loading while an async operation is in flight
  Given a view-model whose data load has not completed
  Then its state is "Loading"

@unit
Scenario: A view-model leaves Loading when its data arrives
  Given a view-model in the "Loading" state
  When the data load completes with items
  Then its state is "Content"

@ui
Scenario: Async views show a skeleton loader, not a blank pane
  Given a view whose data takes time to load
  When I open it
  Then I see skeleton placeholders while it loads
  And I do not see a blank pane

@manual
Scenario: Skeleton loaders match the shape of the content they precede
  Given a list and an editor that load asynchronously
  When each is loading
  Then its skeleton approximates the final layout (rows / editor blocks)
  And the transition to content is not jarring
```

### Error states

```gherkin
@unit
Scenario Outline: Known failures map to a human-readable, actionable error state
  Given an operation fails with "<failure>"
  When the view handles the failure
  Then it shows the message "<message>"
  And it offers the recovery action "<recovery>"
  And no raw stack trace is shown

  Examples:
    | failure           | message                                   | recovery         |
    | missing API key   | No API key configured                     | Open Settings    |
    | offline           | You appear to be offline                  | Retry            |
    | rate limited      | Rate limited — the app is retrying        | Retry            |
    | extraction failed | Could not read this file                  | Re-extract       |

@unit
Scenario: Error states never surface a raw exception message
  Given an operation throws an unexpected exception
  When the view handles the failure
  Then it shows a generic human-readable error
  And the exception detail is written to the log, not the screen

@unit
Scenario: The recovery action re-runs the failed operation
  Given a view in an error state with a "Retry" recovery action
  When I invoke the recovery action
  Then the failed operation is attempted again

@ui
Scenario: A failed load shows an error state with a recovery button, not a crash
  Given a view whose data load fails
  When I open it
  Then I see a styled error message and a recovery button
  And the app does not crash or show a stack trace
```

### Consistency

```gherkin
@manual
Scenario: Empty, loading, and error states are visually consistent across views
  Given the app across its primary views
  Then empty, loading, and error states use the same styled components and tone
  And none shows unstyled default WPF chrome
```
