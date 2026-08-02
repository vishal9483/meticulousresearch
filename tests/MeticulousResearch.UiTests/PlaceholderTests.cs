namespace MeticulousResearch.UiTests;

/// <summary>
/// FlaUI end-to-end tests (@ui) drive the built WPF window and require a real desktop session,
/// so they cannot run in a headless loop. The first real @ui test lands with app-shell-navigation.
/// This placeholder keeps the project green until then.
/// </summary>
public class PlaceholderTests
{
    [Fact(Skip = "No WPF window to drive yet; first @ui test arrives with app-shell-navigation.")]
    public void Ui_smoke_placeholder() { }
}
