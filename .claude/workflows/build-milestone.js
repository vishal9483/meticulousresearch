export const meta = {
  name: 'build-milestone',
  description: 'Build one milestone of MeticulousResearch: for each feature in dependency order, an implementer translates Gherkin to xUnit and codes to green, then an adversarial reviewer gates the merge (repair loop, capped).',
  whenToUse: 'Invoke once per milestone (M0..M6). Pass args:{milestone:"M0", base:"<integration-branch>"}. Autonomous within the milestone; stop and get human sign-off before launching the next.',
  phases: [
    { title: 'Implement' },
    { title: 'Review' },
    { title: 'Integrate' },
  ],
}

// ---------------------------------------------------------------------------
// Config: the dependency-ordered feature list per milestone (from docs/README.md).
// Within a milestone, features are listed in an order that respects intra-milestone
// `Depends on`, so sequential processing on a moving integration branch is safe.
// ---------------------------------------------------------------------------
const MILESTONES = {
  M0: ['app-shell-navigation', 'design-system-theming', 'data-store-migrations', 'settings-secure-key', 'projects-crud'],
  M1: ['text-paste-resource', 'file-upload-extraction', 'url-resource', 'resource-management', 'token-estimation', 'full-text-search', 'context-budget', 'image-vision-caption'],
  M2: ['ai-gateway', 'builtin-file-tools-sandbox', 'conversations', 'model-selector', 'streaming', 'turn-metadata-actions', 'image-attachments', 'rate-limit-backoff', 'prompt-caching'],
  M3: ['artifact-creation', 'deliverable-templates', 'artifact-versioning', 'artifact-diff', 'edit-with-claude', 'report-composition'],
  M4: ['branded-export', 'cost-tracking', 'usage-csv-export', 'backup-restore'],
  M5: ['onboarding', 'empty-loading-error-states', 'accessibility', 'command-palette-shortcuts', 'about-screen'],
  M6: ['app-branding-icon', 'installer', 'update-notice', 'v1-acceptance'],
}

// Features that own a cross-cutting contract — the orchestrator flags these in its
// report so the human reviewer pays extra attention at the milestone gate.
const CONTRACT_FEATURES = new Set(['data-store-migrations', 'ai-gateway', 'model-selector', 'design-system-theming'])

const MAX_REPAIRS = 3

const REVIEW_SCHEMA = {
  type: 'object',
  additionalProperties: false,
  required: ['verdict', 'blocking', 'nonBlocking', 'summary', 'headlessCounts'],
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'REQUEST-CHANGES'] },
    blocking: {
      type: 'array',
      items: {
        type: 'object', additionalProperties: false,
        required: ['finding', 'location', 'gherkinClause'],
        properties: {
          finding: { type: 'string', description: 'What is wrong' },
          location: { type: 'string', description: 'file:line' },
          gherkinClause: { type: 'string', description: 'The Gherkin Then/And clause violated, or "n/a"' },
        },
      },
    },
    nonBlocking: {
      type: 'array',
      items: {
        type: 'object', additionalProperties: false,
        required: ['finding', 'location'],
        properties: { finding: { type: 'string' }, location: { type: 'string' } },
      },
    },
    summary: { type: 'string' },
    headlessCounts: { type: 'string', description: 'Observed dotnet test counts for the headless gate' },
  },
}

// ---------------------------------------------------------------------------
// Prompt builders — refined from the app-shell-navigation hand-run.
// ---------------------------------------------------------------------------
const REPO = 'D:/workdir/MeticulasResearch'

function implementerPrompt(slug, base, branch, priorFindings) {
  const repair = priorFindings
    ? `\n## THIS IS A REPAIR PASS\nA reviewer REQUESTED CHANGES on your previous attempt. Fix EVERY blocking finding below, then re-run the gate and re-commit to ${branch}. Do not regress green tests.\nBLOCKING FINDINGS:\n${priorFindings}\n`
    : ''
  return `You are the IMPLEMENTER agent for MeticulousResearch (.NET 8 WPF desktop app, TDD-by-agent). Implement ONE feature by faithfully translating its pre-written Gherkin into runnable xUnit tests, then writing production code until they pass.

## Assigned feature: ${slug}
Read first (source of truth):
- ${REPO}/docs/features/${slug}/phase.md
- ${REPO}/docs/features/${slug}/tests.md
- ${REPO}/docs/TESTING-STRATEGY.md
- ${REPO}/docs/README.md  (dependency order + cross-cutting contracts)
- The SPEC.md sections cited in phase.md.
${repair}
## Git (IMPORTANT)
You are on branch \`${branch}\`, created from \`${base}\` (all prior approved features in this milestone are already merged into ${base}, so their code/contracts are available — consume them, do not rebuild them). Do your work, then COMMIT everything to \`${branch}\` with a clear message. The reviewer diffs \`${base}..${branch}\`, so an uncommitted tree is invisible — you MUST commit.

## Codebase (already scaffolded — never recreate)
Solution ${REPO}/MeticulousResearch.sln, 6 projects:
- src/MeticulousResearch.Core (net8.0), src/MeticulousResearch.App (net8.0-windows, WPF + CommunityToolkit.Mvvm)
- tests/MeticulousResearch.Core.Tests, tests/MeticulousResearch.App.Tests (net8.0-windows), tests/MeticulousResearch.UiTests (FlaUI), tests/MeticulousResearch.TestSupport (shared fakes: FakeClock, FakeEnvironment exist).
Build/test from ${REPO}:
  dotnet build MeticulousResearch.sln -c Debug
  dotnet test MeticulousResearch.sln -c Debug --filter "Category!=ui&Category!=manual"

## Rules (follow exactly)
1. TEST-FIRST, FAITHFULLY. Translate EVERY Gherkin scenario. Assert exactly what the Then/And clauses say — never soften, skip, tautologize (no Assert.True(true)), or reword to pass. If a scenario is genuinely impossible/wrong, leave the test in place, mark it, and explain in your report — never silently drop it.
2. TRAIT MAPPING (the CI filter depends on it): @unit -> NO Category trait (runs in gate); @ui -> [Trait("Category","ui")] in UiTests (must COMPILE, need not run headless); @manual -> [Trait("Category","manual")] skipped test w/ checklist comment; secondary tags (@integration, @requires-key...) -> extra [Trait]. Scenario Outline -> [Theory] + [InlineData] per Examples row (no missing rows).
3. STAY IN SCOPE. Own only this feature's contracts (see phase.md). Introduce shared interfaces cleanly in Core (or App where WPF-bound) for downstream features to consume. A gesture/behavior that phase.md scopes to a DOWNSTREAM feature may be a loud seam (throw NotSupportedException with a message naming the owning feature) rather than a fake-pass.
4. Push logic into Core/view-models so it's @unit-testable without a window. Constructor-inject VMs so App.Tests can new them with fakes.
5. Match surrounding style: file-scoped namespaces, nullable enabled, XML-doc on public contracts. Directory.Build.props centralizes Nullable/ImplicitUsings — don't re-add per csproj. Add PackageReferences to the project that needs them.
6. Do NOT modify docs/. Do NOT modify global.json, Directory.Build.props, or the CI filter. Do NOT touch other branches.

## Definition of done (from phase.md)
Every @unit scenario GREEN via the headless filter; every @ui scenario written, trait-tagged, and COMPILING (full solution builds 0 errors); no shipped "Not implemented"/blank placeholders; no regression in pre-existing tests. THEN commit to ${branch}.

## Return (raw data for the reviewer, not prose)
- Files created/modified (full paths).
- Scenario-by-scenario map: Gherkin name -> xUnit method (FQN) -> trait(s) -> pass/skip/compile-only.
- Final \`dotnet build\` result and \`dotnet test\` summary counts.
- Any scenario not faithfully implementable + why.
- Contract decisions downstream features depend on.
- The commit SHA you created on ${branch}.`
}

function reviewerPrompt(slug, base, branch) {
  return `You are the REVIEWER agent for MeticulousResearch. An implementer just finished "${slug}" and committed to branch \`${branch}\`. Be ADVERSARIAL: assume it was motivated to make tests pass; find where it cut corners. You do NOT fix anything — you return a structured verdict.

## Source of truth
- ${REPO}/docs/features/${slug}/tests.md  (Gherkin that MUST be faithfully encoded)
- ${REPO}/docs/features/${slug}/phase.md  (deliverables, contract ownership, DoD)
- ${REPO}/docs/TESTING-STRATEGY.md, ${REPO}/docs/README.md

## See exactly what changed
  cd ${REPO} && git diff ${base}..${branch} --stat   then   git diff ${base}..${branch}
(The implementer committed to ${branch}. If the diff is EMPTY, that is itself a BLOCKING finding — nothing was committed.)

## Checklist (cite file:line)
1. FAITHFULNESS (top priority): for EVERY Gherkin scenario, find its xUnit test and verify the assertions encode the Then/And clauses. Flag any that are missing, softened (assert less than stated), tautological (Assert.True(true), asserting a constant, asserting a mock returns what you set), skipped without justification, or reworded to something easier. A green test that doesn't verify the behavior is the #1 thing you hunt for.
2. TRAIT MAPPING: @unit -> no Category; @ui -> [Trait("Category","ui")]; @manual -> [Trait("Category","manual")]; secondary tags as extra traits; Scenario Outline -> [Theory]+[InlineData] with EXACT row count. Verify counts vs the Gherkin.
3. @ui INTEGRITY: @ui tests need not run headless but must COMPILE and genuinely drive the behavior (correct AutomationIds, real assertions), not hollow stubs. If a path is faked/short-circuited (helper throws, failure swallowed), judge whether it's a legitimate cross-feature seam (phase.md scopes it downstream) or a cop-out (a finding).
4. CONTRACT CONSISTENCY: does this feature's owned contract stay coherent/injectable, without leaks that force later features to replace it? Note what will bite named downstream features.
5. SCOPE: stayed within ${slug}; did not weaken shared bootstrap (global.json, Directory.Build.props, CI filter).
6. NO PLACEHOLDERS (DoD + SPEC §9.1(10)): every destination a real designed view.
7. REGRESSIONS: pre-existing smoke tests still pass.

Run the gate yourself:
  cd ${REPO} && git checkout ${branch} && dotnet test MeticulousResearch.sln -c Debug --filter "Category!=ui&Category!=manual"

## Verdict rules
- REQUEST-CHANGES for any faithfulness violation, missing/softened test, broken contract, scope/regression break, shipped placeholder, or empty diff. These go in \`blocking\`.
- Legitimate cross-feature seams, style smells, and latent risks go in \`nonBlocking\`.
- APPROVE only if there are no blocking findings.
Return the structured object. \`blocking\` must be empty iff verdict is APPROVE. Put observed headless test counts in \`headlessCounts\`.`
}

// ---------------------------------------------------------------------------
// Orchestration
// ---------------------------------------------------------------------------
const milestone = (args && args.milestone) || 'M0'
const base = (args && args.base) || 'main'
const features = MILESTONES[milestone]
if (!features) throw new Error(`Unknown milestone "${milestone}". Known: ${Object.keys(MILESTONES).join(', ')}`)

log(`Building ${milestone}: ${features.length} features, integration base = ${base}`)
log(`Sequential on a moving integration branch; each feature merges into ${base} only after an APPROVE.`)

const results = []

for (let i = 0; i < features.length; i++) {
  const slug = features[i]
  const branch = `feat/${slug}`
  log(`[${i + 1}/${features.length}] ${slug}${CONTRACT_FEATURES.has(slug) ? '  (CONTRACT FEATURE — extra scrutiny at gate)' : ''}`)

  // Fresh feature branch off the current (moving) base.
  await agent(
    `cd ${REPO} && git checkout ${base} && git checkout -B ${branch} ${base} && git rev-parse --abbrev-ref HEAD. Then STOP — reply with the branch name only. Do not implement anything.`,
    { label: `branch:${slug}`, phase: 'Implement' }
  )

  // Implementer + capped repair loop, each pass gated by the reviewer.
  let review = null
  let priorFindings = null
  let attempts = 0
  let approved = false

  while (attempts <= MAX_REPAIRS) {
    attempts++
    await agent(implementerPrompt(slug, base, branch, priorFindings), {
      label: attempts === 1 ? `impl:${slug}` : `impl:${slug}#${attempts}`,
      phase: 'Implement',
    })

    review = await agent(reviewerPrompt(slug, base, branch), {
      label: attempts === 1 ? `review:${slug}` : `review:${slug}#${attempts}`,
      phase: 'Review',
      schema: REVIEW_SCHEMA,
    })

    if (review && review.verdict === 'APPROVE') { approved = true; break }

    const blocking = (review && review.blocking) || []
    log(`  ${slug}: REQUEST-CHANGES (attempt ${attempts}) — ${blocking.length} blocking finding(s)`)
    priorFindings = blocking.map((b, n) => `${n + 1}. [${b.location}] ${b.finding} (Gherkin: ${b.gherkinClause})`).join('\n')
  }

  if (approved) {
    // Advance the integration base by merging the approved feature branch.
    await agent(
      `cd ${REPO} && git checkout ${base} && git merge --no-ff --no-edit ${branch} && dotnet test MeticulousResearch.sln -c Debug --filter "Category!=ui&Category!=manual" 2>&1 | tail -20. Report whether the merge succeeded and the post-merge test counts. If the merge has conflicts or tests fail post-merge, report that clearly and do NOT force it.`,
      { label: `merge:${slug}`, phase: 'Integrate' }
    )
    log(`  ${slug}: APPROVED after ${attempts} attempt(s), merged into ${base}.`)
  } else {
    log(`  ${slug}: STILL BLOCKED after ${MAX_REPAIRS} repair attempts — left on ${branch} for human review. Halting milestone to avoid building on a broken contract.`)
  }

  results.push({
    slug,
    branch,
    contractFeature: CONTRACT_FEATURES.has(slug),
    attempts,
    approved,
    verdict: review ? review.verdict : 'NO-REVIEW',
    blocking: (review && review.blocking) || [],
    nonBlocking: (review && review.nonBlocking) || [],
    headlessCounts: review ? review.headlessCounts : null,
    summary: review ? review.summary : null,
  })

  // Fail-stop: a blocked feature poisons everything downstream in the milestone.
  if (!approved) break
}

const approvedCount = results.filter(r => r.approved).length
const blocked = results.find(r => !r.approved)

log(`${milestone} done: ${approvedCount}/${features.length} approved & merged into ${base}.`)

return {
  milestone,
  base,
  approved: approvedCount,
  total: features.length,
  halted: blocked ? blocked.slug : null,
  contractFeaturesInMilestone: features.filter(f => CONTRACT_FEATURES.has(f)),
  gateReminder: `HUMAN GATE: review the merged code on ${base} (especially any contract features) before invoking build-milestone for the next milestone. Push/PR ${base} if you want CI to run the full gate.`,
  features: results,
}
