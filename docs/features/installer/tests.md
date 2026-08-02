# Tests — Signed Windows Installer

**SPEC:** §8 (installer & updates). **Milestone:** M6.
**Depends on:** all functional features

## Traceability
- §8 signed Windows installer (MSIX or WiX/MSI) → Build & signing scenarios.
- §8 app version shown in About → Version scenario (About screen owns the display; here we assert the packaged version matches).
- §3.7 installer branding (product name, icon) → Branding scenarios (visual identity detail lives in `app-branding-icon`).
- §8 non-functional: cold start < 3s → Launch scenario.
- §9.1(1) install via a signed installer and launch to branded first-run onboarding → Install/launch scenarios (end-to-end run lives in `v1-acceptance`).
- §9.1(10) no crashes / no placeholder screens on a clean machine → Clean-machine scenarios.

---

```gherkin
Feature: Signed Windows installer
  As an analyst on a fresh Windows 11 machine
  I want to install MeticulousResearch from a signed installer
  So that I can trust it, run it, and remove it cleanly
```

### Build & packaging

```gherkin
@manual
Scenario: The release build produces a single installer artifact
  Given a Release configuration build of the app
  When the packaging step runs
  Then it produces one installer artifact (MSIX or WiX/MSI)
  And the artifact carries the product name and version

@unit
Scenario: The packaged version matches the assembly version
  Given the release manifest and the built assembly
  Then the installer package version equals the app's assembly/product version
  And that version is the one the About screen reports

@manual
Scenario: The bundled sidecar runtime ships inside the package
  Given the installer artifact
  When its contents are inspected
  Then the Agent SDK sidecar (bundled Node runtime or single-file binary) is included
  And the app can run its primary generation path after install without a separate install step
```

### Code signing

```gherkin
@manual
Scenario: The installer is Authenticode code-signed
  Given the produced installer artifact
  When its digital signature is inspected
  Then it is Authenticode-signed with a valid, trusted code-signing certificate
  And the signature timestamp is present

@manual
Scenario: The main executable is code-signed
  Given the installed application executable
  When its digital signature is inspected
  Then it is signed with the same trusted certificate as the installer

@manual
Scenario: Windows does not warn about an unknown publisher
  Given a clean Windows 11 machine with SmartScreen enabled
  When I run the signed installer
  Then no "unknown publisher" warning is shown
  And the verified publisher name is displayed
```

### Install on a clean machine

```gherkin
@manual
Scenario: Install to a clean Windows 11 machine
  Given a clean Windows 11 (x64) machine with no prior version installed
  When I run the installer and accept the defaults
  Then the app installs without errors
  And a Start Menu entry with the product name and icon is created

@manual
Scenario: First launch reaches branded onboarding
  Given the app has just been installed on a clean machine
  When I launch it from the Start Menu
  Then it opens to the branded first-run onboarding (no crash, no placeholder screen)
  And no API key is required to reach the welcome step

@manual
Scenario: Cold start is within the performance budget
  Given the app is installed on a clean machine
  When I launch it for the first time
  Then it becomes interactive in under 3 seconds

@manual
Scenario: The data directory is created on first run, not at install time
  Given a freshly installed app that has not been launched
  Then no user data directory exists yet
  When I launch and complete onboarding
  Then the data directory is created under %LOCALAPPDATA%/MeticulousResearch/
```

### Uninstall

```gherkin
@manual
Scenario: Uninstall removes the application cleanly
  Given the app is installed
  When I uninstall it via Windows "Apps & features"
  Then the application files and Start Menu entry are removed
  And no orphaned services or background processes remain

@manual
Scenario: Uninstall preserves user data by default
  Given the app is installed and has user projects on disk
  When I uninstall the application
  Then the user data directory under %LOCALAPPDATA% is left intact
  And reinstalling and launching finds the existing projects

@manual
Scenario: Repair / reinstall over an existing install succeeds
  Given a prior version is installed
  When I run the installer for the same or a newer version
  Then it upgrades in place without a manual uninstall
  And existing user data is preserved
```
