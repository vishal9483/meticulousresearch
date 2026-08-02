# Phase — Command Palette & Keyboard Shortcuts

**SPEC:** §3.5. **Milestone:** M5. **Depends on:** app-shell-navigation

## Goal
Provide a Ctrl+K **command palette** to jump to a project or run core create/search actions, plus
consistent **global keyboard shortcuts** (new project/conversation/artifact, send Ctrl+Enter,
stop Esc, search Ctrl+K) so the whole app is drivable from the keyboard (SPEC §3.5). Owns the
**command registry** and the **shortcut binding surface**.

## Deliverables
1. **`ICommandRegistry`** — a catalog of invokable commands, each with an id, display name,
   keywords, and an execute delegate. Includes the static core commands (New project / New
   conversation / New artifact / Search) plus dynamic "Go to project: {name}" entries built from
   `IProjectService.List`.
2. **Palette view-model** — query → ranked results (fuzzy/substring match over name + keywords),
   keyboard selection, an empty-result state, and activation that invokes the chosen command.
3. **Palette view** — styled overlay (design-system tokens) opened by Ctrl+K, focus in the search
   box, arrow/Enter navigation, Esc to dismiss and restore focus.
4. **Global shortcut bindings** — app-level `InputBindings`/`KeyBinding` map: Ctrl+K (palette),
   Ctrl+Enter (send, in composer context), Esc (stop, when streaming), plus new-project/
   conversation/artifact accelerators. Context-aware so send/stop only fire where meaningful.

## Suggested design
- Keep matching/ranking in the palette view-model so it is `@unit`-testable without the window;
  the view is thin wiring proven by `@ui`.
- Commands delegate to existing services/VMs (projects-crud, conversations, artifact-creation,
  full-text-search) — the palette invokes, it does not reimplement.
- Send/stop shortcuts route to the active conversation VM; make Esc a no-op when nothing is
  streaming (proven by test) so it never does anything destructive.
- Register shortcut hints on the commands so tooltips/palette can display them (§3.5
  discoverability, `@manual`).

## Test-first order
1. `@unit` registry + ranking tests (core commands present; queries rank the right result;
   no-match empty state) → `ICommandRegistry` + palette VM.
2. `@unit` command-invocation tests (create commands, jump-to-project navigation) → wire
   delegates to services/VMs.
3. `@unit` send/stop-shortcut tests (Ctrl+Enter sends; Esc stops; Esc idle no-op) → shortcut
   handlers on the composer/conversation VM.
4. `@ui` open/dismiss/keyboard-drive tests → palette overlay + global `InputBindings`.
5. `@manual` discoverability checklist → tooltip/hint pass in the PR.

## Definition of done
- Ctrl+K opens a focused palette; Esc dismisses and restores focus.
- Core commands and jump-to-project results are present, ranked, and invoke the right action;
  no-match shows a designed empty state.
- Ctrl+Enter sends, Esc stops a stream and is a safe no-op otherwise; new-* accelerators work.

## Notes for later features
- `accessibility` verifies the palette and shortcuts are fully keyboard-operable with visible
  focus and correct tab order; keep the palette focus-trap and restore behavior stable.
- New features that add primary actions SHOULD register them in `ICommandRegistry` rather than
  inventing separate entry points.
