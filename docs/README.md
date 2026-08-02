# MeticulousResearch Desktop — Test & Implementation Plan

This directory is the **build plan** for MeticulousResearch Desktop (see [`../SPEC.md`](../SPEC.md)).
It is designed for **test-driven development by coding agents**: each feature is a self-contained
folder holding the **tests** an agent must make pass and the **implementation phase** describing how.

## How this is organized

```
docs/
  README.md                 <- you are here (index + dependency order)
  TESTING-STRATEGY.md       <- Gherkin conventions, test tags, tooling, TDD workflow
  features/
    <feature-slug>/
      tests.md              <- user-perspective Gherkin scenarios (@unit / @ui / @manual)
      phase.md              <- implementation plan to make those tests pass
```

## Working agreement for agents

1. **Read `phase.md` and `tests.md` for your assigned feature, plus `../SPEC.md` sections it cites.**
2. **Write the tests first** (red), then implement until green. Do not modify a test to make it
   pass unless the test is demonstrably wrong — call that out explicitly.
3. **Respect `Depends on`** (below). Do not start a feature whose dependencies are unbuilt.
4. **Stay in your feature's scope.** Shared contracts (interfaces, DB schema) are owned by the
   feature that introduces them; later features consume them.
5. Every scenario is tagged `@unit`, `@ui`, or `@manual` — see [`TESTING-STRATEGY.md`](./TESTING-STRATEGY.md).

## Milestones & feature index (build order)

Milestones are a **build order**, not a scope cut — all of M0–M6 are v1.0 (SPEC §9).
Within a milestone, features are listed in recommended implementation order.

### M0 — Skeleton
| Feature | Depends on | SPEC |
|---------|-----------|------|
| [app-shell-navigation](features/app-shell-navigation/) | — | §4, §7.1 |
| [design-system-theming](features/design-system-theming/) | app-shell-navigation | §3.7 |
| [data-store-migrations](features/data-store-migrations/) | — | §5 |
| [settings-secure-key](features/settings-secure-key/) | data-store-migrations | §3.5, §7.5 |
| [projects-crud](features/projects-crud/) | data-store-migrations, app-shell-navigation | §3.1 |

### M1 — Resources
| Feature | Depends on | SPEC |
|---------|-----------|------|
| [text-paste-resource](features/text-paste-resource/) | projects-crud | §3.2 |
| [file-upload-extraction](features/file-upload-extraction/) | text-paste-resource | §3.2 |
| [url-resource](features/url-resource/) | text-paste-resource | §3.2 |
| [image-vision-caption](features/image-vision-caption/) | file-upload-extraction, ai-gateway | §3.2.1 |
| [resource-management](features/resource-management/) | text-paste-resource | §3.2 |
| [token-estimation](features/token-estimation/) | text-paste-resource | §3.2, §3.6 |
| [full-text-search](features/full-text-search/) | text-paste-resource | §3.1 |
| [context-budget](features/context-budget/) | token-estimation | §3.2, §8 |

### M2 — Q&A
| Feature | Depends on | SPEC |
|---------|-----------|------|
| [ai-gateway](features/ai-gateway/) | settings-secure-key | §7.2, §7.3 |
| [builtin-file-tools-sandbox](features/builtin-file-tools-sandbox/) | ai-gateway | §7.4 |
| [conversations](features/conversations/) | ai-gateway, projects-crud | §3.3 |
| [model-selector](features/model-selector/) | conversations | §6 |
| [streaming](features/streaming/) | conversations | §3.3 |
| [turn-metadata-actions](features/turn-metadata-actions/) | streaming | §3.3 |
| [image-attachments](features/image-attachments/) | conversations, image-vision-caption | §3.2.1 |
| [rate-limit-backoff](features/rate-limit-backoff/) | ai-gateway | §8 |
| [prompt-caching](features/prompt-caching/) | ai-gateway | §8 |

### M3 — Artifacts & templates
| Feature | Depends on | SPEC |
|---------|-----------|------|
| [artifact-creation](features/artifact-creation/) | conversations | §3.4 |
| [deliverable-templates](features/deliverable-templates/) | artifact-creation | §3.4.1 |
| [artifact-versioning](features/artifact-versioning/) | artifact-creation | §3.4 |
| [artifact-diff](features/artifact-diff/) | artifact-versioning | §3.4 |
| [edit-with-claude](features/edit-with-claude/) | artifact-versioning, ai-gateway | §3.4 |
| [report-composition](features/report-composition/) | artifact-creation | §3.4.1 |

### M4 — Deliverable & cost
| Feature | Depends on | SPEC |
|---------|-----------|------|
| [branded-export](features/branded-export/) | artifact-creation, report-composition | §3.4.2 |
| [cost-tracking](features/cost-tracking/) | turn-metadata-actions, model-selector | §3.6 |
| [usage-csv-export](features/usage-csv-export/) | cost-tracking | §3.6 |
| [backup-restore](features/backup-restore/) | projects-crud | §8 |

### M5 — Onboarding & polish
| Feature | Depends on | SPEC |
|---------|-----------|------|
| [onboarding](features/onboarding/) | settings-secure-key, projects-crud | §3.8 |
| [empty-loading-error-states](features/empty-loading-error-states/) | design-system-theming | §3.7 |
| [accessibility](features/accessibility/) | design-system-theming | §8 |
| [command-palette-shortcuts](features/command-palette-shortcuts/) | app-shell-navigation | §3.5 |
| [about-screen](features/about-screen/) | app-shell-navigation | §3.7 |

### M6 — Package & release
| Feature | Depends on | SPEC |
|---------|-----------|------|
| [installer](features/installer/) | all functional features | §8 |
| [app-branding-icon](features/app-branding-icon/) | design-system-theming | §3.7 |
| [update-notice](features/update-notice/) | installer | §8 |
| [v1-acceptance](features/v1-acceptance/) | everything | §9.1 |

## Cross-cutting contracts (introduced early, consumed everywhere)

- **DB schema + migrations** — owned by `data-store-migrations` (§5).
- **`IChatService` / `IArtifactService`** — owned by `ai-gateway` (§7.2); consumed by conversations,
  artifacts, edit-with-claude.
- **Model catalog JSON** — owned by `model-selector` (§6.3); consumed by cost-tracking, ai-gateway.
- **Design system / theme resources** — owned by `design-system-theming` (§3.7).
- **Cost computation** — owned by `cost-tracking` (§3.6); reads tokens persisted by turn/version.
