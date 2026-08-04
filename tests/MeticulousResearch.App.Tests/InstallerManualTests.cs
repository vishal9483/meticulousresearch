namespace MeticulousResearch.App.Tests;

/// <summary>
/// <c>@manual</c> checklist scenarios from docs/features/installer/tests.md (SPEC §8, §3.7,
/// §9.1(1)/(10)). These are inherently manual: they verify the packaged/signed installer on a clean
/// Windows 11 machine. Tagged <c>Category=manual</c> and skipped in the automated gate. The
/// clean-VM checklist is reused as the first scenario of <c>v1-acceptance</c> §9.1(1).
/// </summary>
public class InstallerManualTests
{
    // --- Build & packaging -------------------------------------------------------------------

    // Scenario: The release build produces a single installer artifact
    //   Given a Release configuration build of the app
    //   When the packaging step runs
    //   Then it produces one installer artifact (MSIX or WiX/MSI)
    //   And the artifact carries the product name and version
    //
    // Manual checklist:
    //   [ ] Run installer/build-installer.ps1 -Configuration Release.
    //   [ ] Exactly one artifact is produced: installer/artifacts/MeticulousResearch-<version>.msix.
    //   [ ] The .msix DisplayName is "MeticulousResearch Desktop".
    //   [ ] The .msix Identity version equals installer/version.props MeticulousVersion.
    [Fact(Skip = "@manual — Release packaging step produces one MSIX artifact, verified by a human.")]
    [Trait("Category", "manual")]
    public void The_release_build_produces_a_single_installer_artifact()
    {
    }

    // Scenario: The bundled sidecar runtime ships inside the package
    //   Given the installer artifact
    //   When its contents are inspected
    //   Then the Agent SDK sidecar (bundled Node runtime or single-file binary) is included
    //   And the app can run its primary generation path after install without a separate install step
    //
    // Manual checklist:
    //   [ ] Unpack the .msix (makeappx unpack) — the sidecar payload is present under \sidecar.
    //   [ ] The .NET runtime is included (self-contained publish), no separate runtime install needed.
    //   [ ] After install on a clean machine, run a generation — the primary path works with no
    //       additional install step.
    [Fact(Skip = "@manual — sidecar + runtime bundled in the package, verified by a human.")]
    [Trait("Category", "manual")]
    public void The_bundled_sidecar_runtime_ships_inside_the_package()
    {
    }

    // --- Code signing ------------------------------------------------------------------------

    // Scenario: The installer is Authenticode code-signed
    //   Given the produced installer artifact
    //   When its digital signature is inspected
    //   Then it is Authenticode-signed with a valid, trusted code-signing certificate
    //   And the signature timestamp is present
    //
    // Manual checklist:
    //   [ ] signtool verify /pa /all MeticulousResearch-<version>.msix succeeds.
    //   [ ] The signing certificate chains to a trusted root (no untrusted-cert error).
    //   [ ] The signature carries an RFC-3161 timestamp (signtool shows a timestamp).
    [Fact(Skip = "@manual — installer Authenticode signature + timestamp, verified by a human.")]
    [Trait("Category", "manual")]
    public void The_installer_is_authenticode_code_signed()
    {
    }

    // Scenario: The main executable is code-signed
    //   Given the installed application executable
    //   When its digital signature is inspected
    //   Then it is signed with the same trusted certificate as the installer
    //
    // Manual checklist:
    //   [ ] signtool verify /pa MeticulousResearch.App.exe succeeds.
    //   [ ] The exe's signing certificate is the same one used for the .msix.
    [Fact(Skip = "@manual — main executable code signature matches the installer, verified by a human.")]
    [Trait("Category", "manual")]
    public void The_main_executable_is_code_signed()
    {
    }

    // Scenario: Windows does not warn about an unknown publisher
    //   Given a clean Windows 11 machine with SmartScreen enabled
    //   When I run the signed installer
    //   Then no "unknown publisher" warning is shown
    //   And the verified publisher name is displayed
    //
    // Manual checklist:
    //   [ ] On a clean Win11 machine with SmartScreen on, run the signed installer.
    //   [ ] No "unknown publisher" / unrecognized-app warning appears.
    //   [ ] The verified publisher name "MeticulousResearch" is displayed.
    [Fact(Skip = "@manual — SmartScreen shows a verified publisher, no unknown-publisher warning, verified by a human.")]
    [Trait("Category", "manual")]
    public void Windows_does_not_warn_about_an_unknown_publisher()
    {
    }

    // --- Install on a clean machine ----------------------------------------------------------

    // Scenario: Install to a clean Windows 11 machine
    //   Given a clean Windows 11 (x64) machine with no prior version installed
    //   When I run the installer and accept the defaults
    //   Then the app installs without errors
    //   And a Start Menu entry with the product name and icon is created
    //
    // Manual checklist:
    //   [ ] On a clean Win11 x64 VM with no prior install, run the installer with defaults.
    //   [ ] Install completes without errors and without an admin prompt beyond MSIX's own.
    //   [ ] A Start Menu entry "MeticulousResearch Desktop" with the app icon is created.
    [Fact(Skip = "@manual — clean-machine install + Start Menu entry, verified by a human on a Win11 VM.")]
    [Trait("Category", "manual")]
    public void Install_to_a_clean_windows_11_machine()
    {
    }

    // Scenario: First launch reaches branded onboarding
    //   Given the app has just been installed on a clean machine
    //   When I launch it from the Start Menu
    //   Then it opens to the branded first-run onboarding (no crash, no placeholder screen)
    //   And no API key is required to reach the welcome step
    //
    // Manual checklist:
    //   [ ] Launch from the Start Menu entry.
    //   [ ] The app opens to the branded first-run onboarding (app icon, product name, navy palette).
    //   [ ] No crash and no placeholder/blank screen.
    //   [ ] The welcome step is reachable with no API key entered.
    [Fact(Skip = "@manual — first launch reaches branded onboarding with no key required, verified by a human.")]
    [Trait("Category", "manual")]
    public void First_launch_reaches_branded_onboarding()
    {
    }

    // Scenario: Cold start is within the performance budget
    //   Given the app is installed on a clean machine
    //   When I launch it for the first time
    //   Then it becomes interactive in under 3 seconds
    //
    // Manual checklist:
    //   [ ] On the clean machine, launch the installed app for the first time.
    //   [ ] The onboarding UI becomes interactive in under 3 seconds (measured).
    [Fact(Skip = "@manual — cold start under 3s on a clean machine, verified by a human.")]
    [Trait("Category", "manual")]
    public void Cold_start_is_within_the_performance_budget()
    {
    }

    // Scenario: The data directory is created on first run, not at install time
    //   Given a freshly installed app that has not been launched
    //   Then no user data directory exists yet
    //   When I launch and complete onboarding
    //   Then the data directory is created under %LOCALAPPDATA%/MeticulousResearch/
    //
    // Manual checklist:
    //   [ ] After install but before first launch, %LOCALAPPDATA%/MeticulousResearch/ does NOT exist.
    //   [ ] Launch and complete onboarding.
    //   [ ] %LOCALAPPDATA%/MeticulousResearch/ is now created (data lives outside the install dir).
    [Fact(Skip = "@manual — data directory created on first run, not at install time, verified by a human.")]
    [Trait("Category", "manual")]
    public void The_data_directory_is_created_on_first_run_not_at_install_time()
    {
    }

    // --- Uninstall ---------------------------------------------------------------------------

    // Scenario: Uninstall removes the application cleanly
    //   Given the app is installed
    //   When I uninstall it via Windows "Apps & features"
    //   Then the application files and Start Menu entry are removed
    //   And no orphaned services or background processes remain
    //
    // Manual checklist:
    //   [ ] Uninstall via Settings > Apps & features.
    //   [ ] The application files and the Start Menu entry are removed.
    //   [ ] No orphaned services or background processes (including the sidecar) remain.
    [Fact(Skip = "@manual — uninstall removes app files/Start Menu with no orphaned processes, verified by a human.")]
    [Trait("Category", "manual")]
    public void Uninstall_removes_the_application_cleanly()
    {
    }

    // Scenario: Uninstall preserves user data by default
    //   Given the app is installed and has user projects on disk
    //   When I uninstall the application
    //   Then the user data directory under %LOCALAPPDATA% is left intact
    //   And reinstalling and launching finds the existing projects
    //
    // Manual checklist:
    //   [ ] With user projects created, uninstall the application.
    //   [ ] %LOCALAPPDATA%/MeticulousResearch/ (SQLite DB + project files) is left intact.
    //   [ ] Reinstall and launch — the existing projects are found.
    [Fact(Skip = "@manual — uninstall preserves user data; reinstall finds projects, verified by a human.")]
    [Trait("Category", "manual")]
    public void Uninstall_preserves_user_data_by_default()
    {
    }

    // Scenario: Repair / reinstall over an existing install succeeds
    //   Given a prior version is installed
    //   When I run the installer for the same or a newer version
    //   Then it upgrades in place without a manual uninstall
    //   And existing user data is preserved
    //
    // Manual checklist:
    //   [ ] With a prior version installed, run the installer for the same or a newer version.
    //   [ ] It upgrades in place without requiring a manual uninstall first.
    //   [ ] Existing user data under %LOCALAPPDATA% is preserved after the upgrade.
    [Fact(Skip = "@manual — upgrade-in-place over an existing install preserves data, verified by a human.")]
    [Trait("Category", "manual")]
    public void Repair_reinstall_over_an_existing_install_succeeds()
    {
    }
}
