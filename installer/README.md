# Signed Windows Installer

Packaging for MeticulousResearch Desktop (installer/phase.md, SPEC §8, §3.7). This feature does not
add app functionality — it makes the built app shippable and trustworthy.

## Technology decision — MSIX

MSIX is chosen over WiX/MSI for:

- **Clean lifecycle** — the platform guarantees install / upgrade-in-place / uninstall, so uninstall
  removes app files without bespoke uninstall scripting.
- **Per-user install** to a standard location with no admin prompt beyond what MSIX itself requires.
- **Update story** — MSIX's built-in version/update APIs give `update-notice` (M6) a channel to
  build on; it compares the running version against the latest available.
- **Data preservation** — user data lives outside the package, under
  `%LOCALAPPDATA%/MeticulousResearch/`, so uninstall never deletes user work and reinstall finds it.

## Single-source version

`installer/version.props` (`MeticulousVersion`) is the one place the version is defined. It flows
into:

- the **app assembly** — imported by `src/MeticulousResearch.App/MeticulousResearch.App.csproj`
  (`<Version>$(MeticulousVersion)</Version>`), which the About screen reads via
  `AssemblyAppInfo.Version`;
- the **package manifest** — `installer/AppxManifest.xml` `Identity/@Version` (4-part; first three
  parts equal `MeticulousVersion`).

The installer `@unit` test (`InstallerTests`) reads all three and asserts they agree, so a drift is
caught in the gate.

## Branding hooks

The manifest's `DisplayName` / application name carry the product name
`MeticulousResearch Desktop` (single-sourced from `AssemblyAppInfo.ProductNameValue`) and the logos
reuse the `app-branding-icon` asset `Assets/AppIcon.ico`. This feature consumes those assets; it
does not own them.

## Build & sign

`installer/build-installer.ps1` runs in the **release pipeline** (never on dev machines):

1. `dotnet publish` the WPF app self-contained (x64) so the .NET runtime ships in the package.
2. Stage the bundled Agent SDK sidecar (single-file binary, SPEC §7.2) so the primary generation
   path works after install with no separate install step.
3. Stage the branding icon + manifest.
4. `makeappx pack` → a single `.msix` artifact named with the single-source version.
5. `signtool sign` the exe and the `.msix` with a **timestamped, trusted code-signing certificate**
   so SmartScreen shows a verified publisher.

### Certificate handling

The code-signing certificate is HSM/Trusted-Signing backed and supplied to the pipeline out of band
(`-CertThumbprint` / `CODESIGN_THUMBPRINT`). The private key never lives in the repository and never
touches a developer machine.

## Manual verification

Clean-machine install / launch / uninstall / upgrade and signature verification are inherently
manual and live as `@manual` checklist tests in `InstallerManualTests`. The clean-VM checklist is
reused as the first scenario of `v1-acceptance` §9.1(1).
