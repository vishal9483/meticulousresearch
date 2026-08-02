# Phase — Context Budget

**SPEC:** §3.2, §8. **Milestone:** M1. **Depends on:** token-estimation

## Goal
Before every send, **estimate token usage** (enabled resources + custom instructions + message
overhead) against the **selected model's context window** and the **configured budget**, warn
when exceeded, and **help the user deselect** — with **no silent truncation**.

## Deliverables
1. **`IContextBudgetService`** in Core: `Estimate(projectId, scope, model)` returning an estimate
   breakdown (per-resource contributions + overhead + total) and a status
   (`ok` / `over_budget` / `over_window`).
2. **Window + budget resolution** — read the model's `contextTokens` from the config-driven
   catalog (§6.3) and the configured budget from settings; compare against the estimate.
3. **Overage guidance** — expose largest contributors and a deselect flow that recomputes the
   estimate live; switching model re-resolves the window.
4. **No-truncation guarantee** — the pre-send check blocks/ warns rather than auto-dropping
   resources; generation only proceeds once under window (deselect or model switch resolves it).
5. **Composer budget meter** — estimated tokens vs. window/budget with a warning state and
   actions (deselect / switch model).

## Suggested design
- Reuse `ITokenEstimator` (text + image) from `token-estimation`; sum only **enabled** resources
  (via the resource scope helper) plus a small fixed overhead for instructions/message.
- Keep the estimate/status computation in a Core service + view-model so it's fully `@unit`-testable;
  the meter is thin `@ui` wiring.
- Two thresholds: **configured budget** (soft, user-set) and **model window** (hard ceiling).
  Distinguish the two warnings; window overage cannot be ignored (no truncation).
- Recompute reactively as resources are toggled or the model changes.
- Everything here is an **estimate** and labeled as such (authoritative counts come from usage
  post-send, §3.6).

## Test-first order
1. Estimate composition (enabled + overhead, exclude disabled, include images) `@unit` tests → estimator wiring.
2. Window/budget threshold `@unit` outline (ok / over-budget / over-window) → status logic.
3. Model-switch re-resolution `@unit` test → window from catalog.
4. Deselect-recompute + no-silent-truncation `@unit` tests → guidance + guard.
5. Budget meter + warning actions `@ui` tests → composer wiring.

## Definition of done
- All estimate, threshold, and no-truncation `@unit` scenarios green; `@ui` meter/warning green.
- Estimate excludes disabled resources, includes image tokens + overhead, and is labeled "estimated."
- Over-window never truncates silently; user must deselect or switch model to proceed.

## Notes for later features
- `conversations` / `streaming` (M2) call this pre-send check before dispatching a generation;
  the resource-scope chips drive the enabled scope used here.
- `prompt-caching` (M2) affects real cost but not this pre-send estimate; keep estimate vs.
  authoritative usage (§3.6) clearly separated.
- `model-selector` (M2) supplies the active model whose window this service resolves.
