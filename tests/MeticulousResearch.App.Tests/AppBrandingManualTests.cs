namespace MeticulousResearch.App.Tests;

/// <summary>
/// <c>@manual</c> checklist scenarios from docs/features/app-branding-icon/tests.md (SPEC §3.7,
/// §9.1(1)/(10)). Verified by a human on the packaged/installed app; tagged
/// <c>Category=manual</c> and skipped in the automated gate.
/// </summary>
public class AppBrandingManualTests
{
    // Scenario: The taskbar and Start Menu show the app icon
    //   Given the app is installed and running
    //   Then the taskbar entry shows the application icon
    //   And the Start Menu entry shows the same icon
    //
    // Manual checklist:
    //   [ ] Launch the installed app — the taskbar entry shows the MeticulousResearch icon.
    //   [ ] Open the Start Menu — the app's entry shows the same icon (not a generic default).
    //   [ ] The taskbar and Start Menu icons match the window title-bar icon.
    [Fact(Skip = "@manual — taskbar/Start Menu icon check on the installed app, verified by a human.")]
    [Trait("Category", "manual")]
    public void The_taskbar_and_start_menu_show_the_app_icon()
    {
    }

    // Scenario: The installer displays the product name and icon
    //   Given the signed installer
    //   When I run it
    //   Then it displays the product name "MeticulousResearch Desktop"
    //   And it shows the application icon in its branding
    //
    // Manual checklist:
    //   [ ] Run the signed installer.
    //   [ ] The setup UI displays the product name "MeticulousResearch Desktop".
    //   [ ] The application icon appears in the installer branding.
    [Fact(Skip = "@manual — installer branding check, verified by a human on the signed installer.")]
    [Trait("Category", "manual")]
    public void The_installer_displays_the_product_name_and_icon()
    {
    }

    // Scenario: Brand identity is coherent across the packaged app
    //   Given the installed, packaged app
    //   Then the icon, product name, and navy brand palette are consistent across the title bar,
    //        taskbar, onboarding, About screen, and installer
    //   And nothing shows a generic default icon or a wrong/placeholder name
    //
    // Manual checklist:
    //   [ ] Title bar: shows the app icon and "MeticulousResearch Desktop", navy palette.
    //   [ ] Taskbar & Start Menu: same icon.
    //   [ ] Onboarding welcome: branded (app icon/logo, product name, navy palette), no default chrome.
    //   [ ] About screen: same icon and product name.
    //   [ ] Installer: same icon and product name.
    //   [ ] No generic default icon or wrong/placeholder name appears anywhere.
    [Fact(Skip = "@manual — brand-coherence checklist across the packaged app, verified by a human.")]
    [Trait("Category", "manual")]
    public void Brand_identity_is_coherent_across_the_packaged_app()
    {
    }
}
