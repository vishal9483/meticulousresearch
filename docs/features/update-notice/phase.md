# Phase — Update Notice

**SPEC:** §8 (update mechanism — at minimum an in-app "update available" notice). **Milestone:** M6.
**Depends on:** installer

## Goal
Tell the user, **non-blockingly**, when a newer version is available. Scope is deliberately the
SPEC minimum: a **version comparison** against an advertised latest version plus a dismissible
in-app notice — not a full auto-updater. It must never interrupt work and never surface a raw
error when the check fails.

## Deliverables
1. **`IUpdateService`** — reads the current installed version (same single source `installer`
   uses), fetches the latest advertised version from a configured update source, and returns a
   result: `UpToDate` or `UpdateAvailable(version)`.
2. **Semantic version comparison** — correct ordering across patch/minor/major; malformed or
   unreadable latest strings resolve to "no update," never an error.
3. **Non-blocking notice** — an "update available" notice state (banner/toast) raised through
   the design-system-theming notification surface; non-modal, dismissible.
4. **Dismissal memory** — a dismissed version is not re-notified; a strictly-newer version
   re-raises the notice. Persist the dismissed version via `ISettingsService`.
5. **Silent failure & offline** — check runs off the UI thread; failures (offline, unreachable,
   bad response) are swallowed to "no notice," logged only, per §7.5 clear offline behavior.

## Suggested design
- Keep the whole comparison in Core so it is `@unit`-testable with no network: `IUpdateService`
  takes an injected "latest version provider" that tests can fake (mirrors the `FakeChatService`
  pattern in TESTING-STRATEGY §4). Only a thin adapter actually does the network fetch.
- If `installer` chose **MSIX**, the update source may be the MSIX package/app-installer version
  API; if WiX/MSI, a small version endpoint or manifest. Either way the comparison logic is the
  same and stays behind `IUpdateService`.
- Run the check on startup (and optionally on an interval) but always asynchronously; the UI
  never awaits it. The notice just reflects the latest result state.
- Reuse the toast/banner from `design-system-theming` / `empty-loading-error-states`; do not
  invent new chrome.

## Test-first order
1. `@unit` version-comparison + malformed-input tests → implement the comparison in Core.
2. `@unit` notice-state, dismissal-memory, up-to-date, and silent-failure tests → implement
   `IUpdateService` with a faked latest-version provider.
3. `@ui` non-modal notice + dismiss tests → wire the notice to the notification surface.

## Definition of done
- Version comparison is correct across patch/minor/major and safe on malformed input.
- A newer version raises a non-modal, dismissible notice showing the new version; dismissal is
  remembered per version.
- The check never blocks the UI and never surfaces a raw error when it fails; up-to-date and
  offline both show nothing.

## Notes for later features
- `v1-acceptance` does not require experiencing a real update, but §9.1(10) "no raw errors"
  covers the silent-failure behavior — keep the failure path clean.
- A full download-and-apply auto-updater is out of v1 scope (§8 requires only the notice); if
  added later it can build on `IUpdateService`'s result and the installer/update channel here.
