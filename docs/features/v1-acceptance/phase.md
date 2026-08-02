# Phase — v1.0 Acceptance (the quality bar)

**SPEC:** §9.1 (v1.0 acceptance criteria 1–10). **Milestone:** M6.
**Depends on:** everything (all M0–M5 features + installer, app-branding-icon, update-notice)

## Goal
Prove the **whole product holds together**: a new user on a clean Windows 11 machine can go
install → onboard → key → project-from-template → mixed resources → grounded streaming chat with
per-turn cost → Market Research Report artifact + Edit with Claude + version compare → branded
PDF/DOCX + XLSX → consolidated cost + usage CSV → rate-limit retry without losing work →
backup/restore, with **no crashes, no placeholder screens, and no raw errors**. This feature
adds **no new product functionality**; it is the final acceptance gate that exercises the ten
§9.1 criteria as end-to-end journeys.

## Deliverables
1. **Acceptance suite** — the ten scenarios in `tests.md`, one per §9.1 criterion, as end-to-end
   journeys (not re-tests of unit rules).
2. **Automated `@ui` coverage** — drive the real built app with FlaUI for the criteria that can
   be automated deterministically (5→ export, backup/restore, rate-limit resilience via the
   scripted `FakeChatService`). Reuse the app's DI to inject `FakeChatService`/`IClock` where a
   scenario must be deterministic.
3. **Live end-to-end pass** — a run of the `@requires-network` / `@requires-key` scenarios
   against the **real API** with a real key (the one place TESTING-STRATEGY permits live), to
   confirm grounding, streaming, cost, and model selection work against production.
4. **Clean-VM manual checklist** — the `@manual` criteria (1 install/branding, 10 no
   crashes/placeholders/raw errors) run on a fresh Windows 11 VM from the signed installer, with
   screenshots attached to the PR.
5. **Release sign-off** — a checked-off record in the PR that all ten criteria passed, gating v1.0.

## Suggested design
- **Do not duplicate unit rules.** Each capability's own feature `tests.md` owns the fine-grained
  `@unit` cases; here, assert the user-visible end-to-end outcome only. If an acceptance scenario
  fails, the fix belongs in the owning feature, not here.
- **Determinism where possible:** criteria 6 (export), 8 (rate-limit), and 9 (backup/restore) are
  fully deterministic offline — automate them `@ui` with `FakeChatService` (429-then-success for
  criterion 8) and an injected `IClock` for backoff timing. Only 2, 3, 4, 5, 7 need the live API.
- **Sequence as a journey:** the scenarios share a narrative (same project carried forward), so
  run them in §9.1 order; a single fixture project can flow from creation (2) through backup (9).
- **Clean machine is real:** criteria 1 and 10 must run on a fresh VM from the actual signed
  installer produced by `installer` — not on a dev box — since they gate "install" and "no
  placeholders/crashes on a clean machine."

## Test-first order
Because this depends on everything, "test-first" here means writing the acceptance scenarios as
the executable definition of the v1.0 bar, then verifying:
1. Author all ten scenarios (this `tests.md`) as the shared definition of done for v1.0.
2. Automate the deterministic `@ui` criteria (6, 8, 9) against the built app.
3. Automate the live `@ui @requires-network @requires-key` criteria (2, 3, 4, 5, 7) with a real key.
4. Execute the `@manual` clean-VM criteria (1, 10) from the signed installer; attach evidence.

## Definition of done
- All ten §9.1 criteria pass: automated `@ui` green, live end-to-end run green with a real key,
  and `@manual` clean-VM checklist checked off with screenshots.
- The full install→…→backup/restore journey completes on a fresh Windows 11 machine with no
  crashes, no unstyled/placeholder screens, and no raw errors.
- No regression in any previously-green feature test (TESTING-STRATEGY §5.4).
- v1.0 release is signed off in the PR.

## Notes for later features
- This is the last gate; there are no later features. Any failure re-opens the owning feature.
- Keep the clean-VM checklist and the live-run script as the reusable **release regression pass**
  for future versions.
