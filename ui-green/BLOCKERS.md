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
| B1 | P3 | `ImageAttachmentsUiTests` (2) | UNIMPLEMENTED | Per-turn image attachments + composer thumbnail not rendered; test 2 needs a seeded turn-with-attachment on open (risks the 18 green convo tests) and a pending composer attachment without a file dialog (UIA can't drive one). | Build turn/composer thumbnail + click-to-preview overlay; seed a captioned attachment turn behind the @ui flag. | `Views/ConversationsView.xaml` + MASTER seed |
| B2 | P3 | `ToolTransparencyUiTests` (1) | SEAM | No tool-call `ChatEvent`; the fake can't emit tool calls, so per-turn `TurnToolActivity` (Read/Write) never renders. | MASTER: emit tool-call events from `FakeChatService`; wire `ToolCallLog`/`ToolCallRecord` into turn rendering. | `Views/ConversationsView.xaml` + MASTER `FakeChatService` |

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
