# MeticulousResearch Desktop — Client Delivery Checklist

> Generated during the delivery-readiness pass on **2026-08-04**.
> Automated engineering gates are **green**; the items below are the human / release-gate steps that
> cannot be executed by an automated agent (they need an interactive desktop, a clean VM, a real API
> key + network, or a signing certificate).

---

## Status summary

| Gate | State | Notes |
|------|-------|-------|
| Build (`Debug`) | ✅ Green | 0 warnings / 0 errors, all 6 projects |
| Headless CI gate (`Category!=ui&Category!=manual`) | ✅ Green | 646 passed, 2 intentional skips |
| E2E suite committed | ✅ Done | branch `test/e2e-suite` |
| FlaUI launch-path bug | ✅ Fixed | `ShellUiFixture.ResolveAppExePath` |
| FlaUI release-gate journeys (`@ui`) | ⚠️ Manual | must be run on an interactive desktop — see step 2 |
| Live-API acceptance (J-13, direct-API round trip) | ⚠️ Manual | paid network call, key-gated — see step 3 |
| Manual V1 criteria 1 & 10 | ⚠️ Manual | clean-VM + signed installer — see step 4 |
| Signed MSIX build | ⚠️ Manual | release pipeline + cert — see step 4 |
| Merge `test/e2e-suite` → `main` + tag `v1.0.0` | ⛔ Blocked | do last, after 2–4 pass — see step 5 |

Everything below is **manual**. Do them in order; do not tag the release until all pass.

---

## Step 2 — Run the FlaUI release-gate journeys (interactive desktop)

These drive the real WPF window via FlaUI/UIA3 and are **compile-only in the CI gate** by design
(`@ui` = "must compile headless; need not run"). They must be run by a human on a machine with an
interactive desktop session (not over headless SSH / CI agent without a desktop).

A launch bug was fixed in this pass (the harness looked for the app under a doubled
`bin/net8.0-windows/net8.0-windows/` path); the app now launches correctly.

```powershell
# From repo root, on an interactive Windows desktop session:
dotnet build MeticulousResearch.sln -c Debug
dotnet test tests/MeticulousResearch.UiTests/MeticulousResearch.UiTests.csproj -c Debug --filter "Category=ui"
dotnet test tests/MeticulousResearch.E2E/MeticulousResearch.E2E.csproj -c Debug          # includes the FlaUI @e2e journeys
```

**Expected / to verify by hand:**
- The app window appears and each journey navigates it end to end.
- Some journey helpers fail loudly at cross-feature seams (e.g.
  `NotSupportedException: Opening a project requires projects-crud; wire this helper to its open
  action.`) when an expected `AutomationId` (`WorkspaceRoot`, `ProjectsHomeRoot`, `MessageInput`, …)
  is not present in the current app state. For each failure, confirm whether it is (a) an app wiring
  gap (missing `AutomationId` on a real control) or (b) a test-harness gap, and fix the owning side.
  Track these as release-gate defects, not merge-gate defects.

**Sign-off:** _______________________  Date: ____________  Result: ☐ Pass ☐ Fail (attach screenshots)

---

## Step 3 — Live-API acceptance (J-13 + direct-API round trip)

These are `[Fact(Skip = …)]` in code because they make **real, billable** calls to the Anthropic API
and require network. They are intentionally excluded from every automated run. Run them by hand as
the release gate.

Tests involved:
- `MeticulousResearch.E2E/Journeys/J13_LiveApiAcceptance.cs`
- `MeticulousResearch.Core.Tests/Ai/DirectApiFallbackTests.Direct_api_real_round_trip`

**Prerequisites (do NOT commit these):**
- A valid `ANTHROPIC_API_KEY` in the environment. (Env-first resolution wins over persisted
  settings; the key is never written to SQLite/settings/command line.)
- Optionally `ANTHROPIC_BASE_URL` if routing through a gateway/proxy.
- Network access to the endpoint.

**How to run (temporarily un-skip, run, then revert):**
1. In a scratch checkout, remove the `Skip = "…"` argument from the two `[Fact]`/`[Fact(Skip=…)]`
   attributes above (or change them to a plain `[Fact]`).
2. Run just those tests:
   ```powershell
   dotnet test tests/MeticulousResearch.E2E/MeticulousResearch.E2E.csproj -c Debug `
     --filter "FullyQualifiedName~J13_LiveApiAcceptance"
   dotnet test tests/MeticulousResearch.Core.Tests/MeticulousResearch.Core.Tests.csproj -c Debug `
     --filter "FullyQualifiedName~DirectApiFallbackTests.Direct_api_real_round_trip"
   ```
3. **Revert the un-skip** — do not merge a change that removes the `Skip` (it would fire paid calls
   in the gate).

> ⚠️ Cost/security: these calls are billed to the supplied key and hit the live service. An agent
> must not run them or handle the key value. A human runs this step.

**Sign-off:** _______________________  Date: ____________  Result: ☐ Pass ☐ Fail

---

## Step 4 — Build the signed installer & verify V1 criteria 1 and 10 on a clean VM

Two of the ten SPEC §9.1 acceptance criteria are `@manual` (they cannot be automated):
- **Criterion 1** — install via the **signed installer** and launch to branded onboarding.
- **Criterion 10** — run the whole workflow with **no crashes, no placeholders, no raw errors**.

Tests holding the checklists:
- `tests/MeticulousResearch.App.Tests/V1AcceptanceManualTests.cs`
- `tests/MeticulousResearch.Core.Tests/**` (`@manual` branding/accessibility checklists)
- `installer/README.md` "Manual verification" + `InstallerManualTests`

### 4a. Build & sign the MSIX (release pipeline only — never a dev machine)

Version is single-sourced from `installer/version.props` → currently **`1.0.0`**.

```powershell
# On the release/signing machine, with the code-signing cert available:
$env:CODESIGN_THUMBPRINT = "<thumbprint of the trusted, HSM/Trusted-Signing cert>"
./installer/build-installer.ps1 -Configuration Release -Runtime win-x64
# -> installer/artifacts/MeticulousResearch-1.0.0.msix (signed + timestamped)
```

Verify the signature shows a **verified publisher** (SmartScreen) and the certificate is trusted and
timestamped. The private key must never touch the repo or a developer machine.

### 4b. Clean-VM acceptance (Criterion 1)

On a **fresh Windows VM** (no prior install, no leftover `%LOCALAPPDATA%/MeticulousResearch/`):
1. Install the signed `.msix`; confirm no unexpected admin prompts and a verified publisher.
2. Launch → confirm branded onboarding appears (product name **MeticulousResearch Desktop**, app
   icon, correct version `1.0.0` on the About screen).
3. Confirm upgrade-in-place and uninstall behave (uninstall must not delete user data under
   `%LOCALAPPDATA%/MeticulousResearch/`).

### 4c. Full-workflow no-crash pass (Criterion 10)

On the installed app, run the complete analyst journey and confirm **no crash / no placeholder text /
no raw error dialogs** at any step:
> create project → add resources (paste / file / URL / image) → grounded conversation with streaming
> → create artifact → version & diff → edit-with-Claude → compose report → branded export → check
> cost tracking & CSV export → backup & restore.

Capture screenshots at each stage for the PR record.

**Sign-off (4a build):** ______________  **(4b install):** ______________  **(4c workflow):** ______________
Date: ____________  Result: ☐ Pass ☐ Fail

---

## Step 5 — Merge and tag the release (do this LAST)

Only after steps 2–4 are signed off:

```powershell
# Integrate the E2E suite + UI harness fix:
git checkout main
git merge --no-ff test/e2e-suite
dotnet build MeticulousResearch.sln -c Debug
dotnet test MeticulousResearch.sln -c Debug --filter "Category!=ui&Category!=manual"   # must be green

# Tag the release (version matches installer/version.props):
git tag -a v1.0.0 -m "MeticulousResearch Desktop v1.0.0"

# Push only when the team decides to publish (nothing is pushed automatically):
# git push origin main --follow-tags
```

**Release sign-off:** _______________________  Date: ____________

---

## What was done automatically in this pass

- Committed the E2E journey suite (J00–J24), `E2E-TEST-SUITE.md`, and the `.sln` registration to
  branch `test/e2e-suite`.
- Fixed `ShellUiFixture.ResolveAppExePath` so the FlaUI harness launches the built app.
- Verified: build clean; headless gate green (646 passed / 2 intentional skips).
