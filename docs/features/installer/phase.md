# Phase — Signed Windows Installer

**SPEC:** §8 (installer & updates; +§3.7 installer branding). **Milestone:** M6.
**Depends on:** all functional features (packages a complete app)

## Goal
Produce a **signed Windows installer** that installs the finished app to a clean Windows 11
machine, launches to branded onboarding, and uninstalls cleanly. This feature packages the
whole product — it does not add app functionality; it makes the built app shippable and
trustworthy.

## Deliverables
1. **Packaging pipeline** — choose MSIX **or** WiX/MSI and produce a single installer artifact
   from a Release build. Include the WPF app, the .NET runtime (self-contained or framework
   dependency declared), and the **bundled Agent SDK sidecar** (bundled Node runtime or
   single-file binary via `pkg` / `node --experimental-sea`, per §7.2).
2. **Versioning** — the package version is derived from a single source (assembly / product
   version) so the installer, the executable, and the About screen all agree.
3. **Code signing** — Authenticode-sign the installer and the main executable with a trusted
   code-signing certificate, with a timestamp, so SmartScreen shows a verified publisher.
4. **Install experience** — Start Menu entry with product name + icon; per-user install to a
   standard location; no admin prompt beyond what the chosen technology requires.
5. **Clean lifecycle** — install / upgrade-in-place / uninstall all work; uninstall removes app
   files but **preserves user data** under `%LOCALAPPDATA%/MeticulousResearch/` by default.
6. **Branding hooks** — installer surfaces the product name and app icon (asset ownership is in
   `app-branding-icon`; this feature consumes those assets).

## Suggested design
- Prefer **MSIX** for clean install/uninstall, per-user install, and a straightforward update
  story that `update-notice` can build on; fall back to **WiX/MSI** if the sidecar/runtime
  packaging is simpler that way. Record the decision in the PR.
- Keep the **version** in one place (e.g. `Directory.Build.props`) and flow it into the package
  manifest and the assembly so the `@unit` "versions match" test reads both from the same source.
- Data lives outside the install dir (in `%LOCALAPPDATA%`) precisely so uninstall never deletes
  user work — do not put the SQLite DB or project files under the install root.
- Signing runs in the release pipeline, not on dev machines; document the cert handling in the PR.

## Test-first order
1. `@unit` version-match test → wire a single version source into manifest + assembly.
2. `@manual` build/packaging checklist → stand up the packaging pipeline; produce the artifact.
3. `@manual` signing checklist → add Authenticode signing to installer + exe.
4. `@manual` clean-machine install / launch / uninstall / upgrade checklist → verify on a
   clean Windows 11 VM (the one place this is inherently manual).

## Definition of done
- One signed installer artifact is produced from a Release build; installer and exe are
  Authenticode-signed with a trusted cert and no unknown-publisher warning.
- Fresh install on a clean Win11 VM launches to branded onboarding in < 3s, with the sidecar
  present and generation working.
- Uninstall removes the app but keeps user data; upgrade-in-place preserves data.
- Packaged version equals the About-screen version.

## Notes for later features
- `update-notice` depends on this: it compares the running version (same single source) against
  the latest available and points the user at the installer/update channel chosen here.
- `v1-acceptance` §9.1(1) exercises the real install→onboard path end-to-end on a clean machine;
  keep the clean-VM checklist reusable as that gate's first scenario.
- If MSIX is chosen, its built-in update/version APIs may satisfy part of `update-notice`; note
  that in the PR.
