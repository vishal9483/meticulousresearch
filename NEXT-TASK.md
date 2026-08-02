# NEXT TASK — run one feature through the agent pipeline (fresh-context handoff)

> Temporary handoff file. Read this top-to-bottom, do the work, then delete this file
> (`git rm NEXT-TASK.md`) as the final step. Everything you need is here or in `docs/`.

## TL;DR of what to do
1. Create a project `CLAUDE.md` (persistent project instructions) — see §A below.
2. Create two reusable subagent definitions (`implementer`, `reviewer`) under `.claude/agents/` — see §B.
3. Build **one** feature — `data-store-migrations` — via the implementer→reviewer→merge loop — see §C.
4. Report back at the human gate; do NOT continue to other features. See §D.

---

## Background (why we're here)
MeticulousResearch is a .NET 8 WPF desktop app being built **TDD-by-agent**: every feature already
has hand-written Gherkin specs in `docs/features/<slug>/{tests.md,phase.md}`. The agreed pipeline is
**implementer + independent adversarial reviewer** (NOT coder-vs-tester), orchestrated by a
deterministic Workflow script, **milestone-gated** (human sign-off between milestones).

Already done and committed on branch `feat/app-shell-navigation` (current HEAD `b6440b4`):
- **Phase 0 bootstrap** (`fc0420d`): solution + 6 projects, CI gate, cross-cutting fakes.
- **First feature `app-shell-navigation`** (`d75af5e`): implementer+reviewer hand-run, APPROVED.
- **Orchestrator** (`b6440b4`): `.claude/workflows/build-milestone.js` — DAG-walking, per-feature
  implementer→reviewer repair loop (capped 3), approved features merge into a moving integration base.

Full detail is in memory: `agent-build-pipeline.md` (and `meticulousresearch-product.md`,
`api-key-endpoint-resolution.md`). Read those memory files first.

## Repo facts you need
- Working dir: `D:\workdir\MeticulasResearch`. Shell is bash on Windows (use `/dev/null`, forward slashes).
- Solution: `MeticulousResearch.sln`, 6 projects (Core net8.0; App net8.0-windows WPF+CommunityToolkit.Mvvm;
  Core.Tests; App.Tests net8.0-windows; UiTests FlaUI; TestSupport with FakeClock/FakeEnvironment).
- Only .NET SDK 7 and 9 installed; `global.json` rolls forward and builds `net8.0` fine.
- Build:  `dotnet build MeticulousResearch.sln -c Debug`
- Gate:   `dotnet test MeticulousResearch.sln -c Debug --filter "Category!=ui&Category!=manual"`
- **Trait convention** (the gate depends on it): `@unit` → no Category trait; `@ui` →
  `[Trait("Category","ui")]` (compile-only headless); `@manual` → `[Trait("Category","manual")]`
  skipped w/ checklist; secondary tags → extra traits; Scenario Outline → `[Theory]`+`[InlineData]`.
- Commit trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Everything is on LOCAL branches; nothing pushed. Don't push unless asked.

---

## §A — Create `CLAUDE.md` (project instructions)
Create `D:\workdir\MeticulasResearch\CLAUDE.md` capturing the durable project conventions so every
future session (and `/init`-style tooling) has them. Include, concisely:
- What the project is (one line) + pointer to `SPEC.md` and `docs/README.md` as sources of truth.
- Build & test commands and the `Category!=ui&Category!=manual` headless gate.
- The trait mapping convention (above) — this is easy to get wrong and breaks CI.
- The TDD-by-agent workflow: specs in `docs/features/<slug>/`, implementer→reviewer topology,
  `.claude/workflows/build-milestone.js` orchestrator, milestone gates, contract features
  (`data-store-migrations`, `ai-gateway`, `model-selector`, `design-system-theming`) get extra scrutiny.
- Repo conventions: file-scoped namespaces, nullable enabled, XML-doc on public contracts,
  `Directory.Build.props` centralizes Nullable/ImplicitUsings (don't re-add per csproj), commit trailer.
- Never modify `docs/`, `global.json`, `Directory.Build.props`, or the CI filter from a feature.
Keep it tight (it loads into every context). Commit it.

## §B — Create reusable subagent definitions under `.claude/agents/`
The orchestrator currently inlines its prompts. Extract them into two reusable agent definitions so
they're versioned and referencable (via the Agent tool's `agentType`, and by the orchestrator later).
Create:
- `.claude/agents/implementer.md`
- `.claude/agents/reviewer.md`

Each is a markdown file with YAML frontmatter (`name`, `description`, optional `tools`, `model`) then
the system prompt body. **Source the prompt text verbatim from the proven prompts** in
`.claude/workflows/build-milestone.js` (functions `implementerPrompt` and `reviewerPrompt`) — those
were refined during the hand-run and are known-good. Generalize the hardcoded feature slug/branch into
instructions that read the assignment from the task prompt. Keep the critical rules intact:
- Implementer: faithful Gherkin→xUnit, trait mapping, stay-in-scope, loud cross-feature seams,
  **commit to the feature branch** (so the reviewer can diff), return raw data.
- Reviewer: adversarial, faithfulness-first, cite file:line, structured APPROVE/REQUEST-CHANGES verdict,
  empty diff = blocking, run the gate itself.
After creating them, verify the orchestrator still works by referencing these (optional: update
`build-milestone.js` to pass `agentType:'implementer'`/`'reviewer'` to its `agent()` calls — only if
it stays behaviorally identical; otherwise leave the inline prompts and treat the agent files as the
canonical copy). Commit.

## §C — Build ONE feature: `data-store-migrations`
This is the DB-schema contract feature (everything downstream reads it) — hence run it alone, with care.
Its deps are satisfied (none, or already-merged app-shell). Read
`docs/features/data-store-migrations/{phase.md,tests.md}` and the SPEC §5 sections it cites first.

Two ways to run it — **prefer the orchestrator** for consistency, fall back to manual if needed:

**Option 1 (preferred): invoke the orchestrator for just this feature.**
The current script builds a whole milestone. Either (a) temporarily run it with a one-feature list, or
(b) drive the loop manually with the agent definitions from §B. Simplest: launch the `implementer`
agent, then the `reviewer` agent, honoring the repair loop (cap 3), then merge on APPROVE. Use base
branch `feat/app-shell-navigation` (the current integration tip). Create `feat/data-store-migrations`
off it; implementer commits there; reviewer diffs `feat/app-shell-navigation..feat/data-store-migrations`;
on APPROVE, merge into `feat/app-shell-navigation` and re-run the gate.

**Option 2 (manual, if orchestration misbehaves):** run the two agents yourself with the prompts from
§B, same branch/commit/diff/merge discipline.

Guardrails:
- Independently verify the implementer's claims (run build + gate yourself; don't trust the self-report).
- `data-store-migrations` is a CONTRACT feature — scrutinize the schema/migration API shape the reviewer
  approves, since later features depend on it.
- If still REQUEST-CHANGES after 3 repairs, STOP and surface it; don't force a merge.

## §D — Report at the gate (do NOT auto-continue)
When the feature is APPROVED and merged, STOP and report to the user:
- Verdict + attempts, final gate test counts (verified), files/contract added.
- The schema/migration contract decisions downstream features will consume.
- Then ask whether to continue to the next M0 feature or pause.
Do NOT proceed to other features without the user's go-ahead (milestone-gated policy).

## Final step
`git rm NEXT-TASK.md` and commit its removal (this file is a one-shot handoff).
