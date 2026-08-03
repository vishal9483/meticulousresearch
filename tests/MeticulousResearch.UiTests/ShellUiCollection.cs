namespace MeticulousResearch.UiTests;

/// <summary>
/// Serializes the @ui tests that each launch the WPF window so they don't fight over the desktop.
/// </summary>
[CollectionDefinition("shell-ui")]
public sealed class ShellUiCollection : ICollectionFixture<ShellUiFixture>
{
}
