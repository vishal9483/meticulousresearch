# UI-Green BLOCKERS & REMAINING-WORK ledger

The **master maintains this file** during autonomous long-running sessions. It is the durable record
of every test that is **not yet green** and *why*, so the run never stalls silently and a human can
pick up any residue later. Update it after every packet iteration alongside `STATUS.md`.

Rules:
- A packet is **never allowed to halt the whole run**. When a test cannot be greened honestly after
  the repair cap (3 passes), the master records it here as a BLOCKER and moves to the next packet.
- **Never** soften/skip/tautologize an assertion to clear a blocker. A blocker stays red and logged.
- Each entry must be actionable: what is missing, why it's blocked, and the smallest next step.
- When a blocker is later resolved, move it to the "Resolved" section with the fixing commit SHA.

## Blocker categories
- `UNIMPLEMENTED` — the app feature/affordance the test asserts does not exist yet and must be built.
- `SEAM` — needs a backend/fake/seed seam that isn't wired (may require a MASTER shared-infra change).
- `ENV` — cannot run headlessly/here (interactive desktop, live API key, signed installer, clean VM).
- `FLAKY` — passes intermittently; needs a deterministic wait/seed.

---

## OPEN blockers (autonomous run keeps going past these)

| ID | Packet | Test(s) | Category | Why blocked | Smallest next step | Owner file |
|----|--------|---------|----------|-------------|--------------------|------------|
| B1 | P3 | `ImageAttachmentsUiTests` (2) | UNIMPLEMENTED | Per-turn image attachments + composer thumbnail not rendered; test 2 needs a seeded turn-with-attachment on open and a pending composer attachment without a file dialog (UIA can't drive one). | Build turn/composer thumbnail + click-to-preview overlay; seed a captioned attachment turn behind the @ui flag. | `Views/ConversationsView.xaml` + MASTER seed |
| B2 | P3 | `ToolTransparencyUiTests` (1) | SEAM | No tool-call `ChatEvent`; the fake can't emit tool calls, so per-turn `TurnToolActivity` never renders. | MASTER: emit tool-call events from `FakeChatService`; wire `ToolCallLog`/`ToolCallRecord` into turn rendering. | `Views/ConversationsView.xaml` + MASTER `FakeChatService` |
| B3 | P5 | `AccessibilityUiTests` (4) | UNIMPLEMENTED | Needs a modal New-project dialog (`NewProjectDialogRoot`) with focus trap + restore (`DialogLastControl`), keyboard-only reachability, and a keyboard `FocusAdorner`. The home has an inline create form, not a modal dialog; `NewProjectButton` opens the template gallery, not a focus-trapping dialog. Cross-packet (dialog spans shell + home). | Build a modal New-project dialog hosting the gallery with a WPF focus scope (trap+restore) and keyboard focus-visual. | `MainWindow.xaml` + `Views/ProjectsHomeView.xaml` |
| B4 | P5 | `CommandPaletteUiTests.Esc_closes_the_command_palette` (1) | SEAM | Assertion requires `CommandPaletteOverlay` to remain in the UIA tree *after* close (offscreen). Every element that stays findable-when-closed must be Visible, and a persistent full-surface Visible overlay regressed ~40 shared-session content tests (hit-test interference). | Needs a non-covering persistent surfacing proxy for the overlay id, or a test-side change (out of scope). | `MainWindow.xaml` |
| B5 | P5 | `EmptyLoadingErrorStatesUiTests` (6) | SEAM | The @ui harness seeds a sample project so "Projects home" is never empty; per-section views use per-section ids (`ArtifactsEmptyState`…) not the generic `EmptyState`/`SkeletonLoader`/`ErrorState`; and the test navigates by section name which needs a project open (screen-presence) and a load-failure/loading seam. | Add generic empty/skeleton/error ids to each section + an @ui load-fail/loading seam + an empty-home path. | shared controls + section views |
| B6 | P5 | `UpdateNoticeUiTests` (2) | SEAM | About is reachable (Settings mounted → OpenAboutButton), but the `UpdateNotice` shows only when an update is available; the @ui harness has no available-update seam, so `IsUpdateNoticeVisible` is false. | Add an @ui fake update service that reports an available update (MASTER `ServiceConfiguration`/`App.xaml.cs`). | `AboutView` + MASTER DI |
| B7 | P6 | `DeliverableTemplatesUiTests.The_New_artifact_flow...` (1) | SEAM | `NewArtifactButton` lives in the Artifacts section (P2 view) and the test doesn't navigate there (screen-presence); also the New-artifact flow currently creates an artifact directly rather than surfacing the gallery (changing it risks P2's green ArtifactCreation tests). | Route the New-artifact flow through the gallery + make it reachable without navigation (mount or nav) without regressing P2. | `Views/ArtifactsView.xaml` (P2) |
| B8 | P6 | `DeliverableTemplatesUiTests.The_New_project_flow...` (1) | FLAKY | Passes in isolation. In the shared session it depends on the app being on the Projects home; prior tests (e.g. BackupRestore.Backing_up) leave the workspace open, so `ByName("New project")` isn't found. Wiring is correct (New project → gallery). | Make "New project" reachable from any screen without regressing Accessibility's "New project"→dialog expectation, or a test-side EnsureAtHome (out of scope). | `Views/ProjectsHomeView.xaml` |
| B9 | — | `ReportCompositionUiTests` (2) | SEAM | Report-composition view (add-artifact-as-section, drag-to-reorder) not reachable/wired in the @ui shell; not owned by any P1–P7 packet cluster. | Identify the report-composition entry point + wire/reach it; assign an owner. | `Views/ReportCompositionView.xaml` |
| B10 | P7 | `V1AcceptanceUiTests` (9) | ENV / SEAM | End-to-end journeys spanning many surfaces. Several carry `requires-network`/`requires-key` (live API generation, model listing, key validation) → ENV. The rest need surfaces not wired for @ui: the onboarding **API-key step** (`OnboardingApiKeyStep`/`KeyValidationSuccess`/`AvailableModelsList`/`FinishOnboardingButton`), template→project scaffold creation, and full chat/export/backup chains. | Wire the onboarding stepper + a fake key-tester/model-lister behind the @ui flag; run live criteria on a keyed desktop. | onboarding + MASTER seams |


## REMAINING packets (not blockers — just not started; autonomous run works these next)

| Packet | State | Scope summary |
|--------|-------|---------------|
| P4-dashboard | TODO | `UsageCsvExport` — dashboard already surfaces the ids; verify export click path + confirmation retry-wait. |
| P5-shell | TODO | CommandPalette, Theme, Accessibility, EmptyLoadingErrorStates (generic ids), AppBranding, Settings, UpdateNotice, Onboarding. |
| P6-projects | TODO | BackupRestore + DeliverableTemplates entry points. |
| P7-v1 | BLOCKED-BY-ORDER | V1Acceptance journeys — start only after P1–P6 green. Some journeys may become ENV blockers (live API / installer). |

## RESOLVED (move entries here with the fixing commit)

- _(none yet)_

## HUMAN-ONLY residue (surfaced from `build-progress.md` — do NOT attempt headlessly)
- Live-API round trips (`@requires-key`, J-13/direct-api) — paid/network; need a real key on a human desktop.
- `@manual` V1 acceptance 1 & 10 — signed installer + full no-crash workflow on a clean VM.
- Any `@ui` journey that genuinely needs an interactive desktop session.
