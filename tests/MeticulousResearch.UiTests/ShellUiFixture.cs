using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace MeticulousResearch.UiTests;

/// <summary>
/// Launches the built WPF app and exposes its main window for FlaUI-driven @ui tests. Requires a
/// real desktop session, so these tests do not run in the headless gate — but they must compile.
/// </summary>
public sealed class ShellUiFixture : IDisposable
{
    private readonly Application _app;
    private readonly UIA3Automation _automation;

    public ShellUiFixture()
    {
        _automation = new UIA3Automation();
        _app = Application.Launch(ResolveAppExePath());
        MainWindow = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(20))
                     ?? throw new InvalidOperationException("The app main window did not appear.");
    }

    /// <summary>The app's main shell window.</summary>
    public Window MainWindow { get; }

    /// <summary>Resolves the built App exe next to the test output (same Debug/Release config).</summary>
    private static string ResolveAppExePath()
    {
        var config = Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory)!.TrimEnd(Path.DirectorySeparatorChar));
        // tests/.../bin/<config>/net8.0-windows -> repo root
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "src", "MeticulousResearch.App", "bin", config,
            "net8.0-windows", "MeticulousResearch.App.exe");
        return exe;
    }

    public void Dispose()
    {
        try
        {
            if (!_app.HasExited)
            {
                _app.Close();
                _app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(2));
            }
        }
        catch (Exception)
        {
            // Best-effort teardown.
        }
        finally
        {
            _app.Dispose();
            _automation.Dispose();
        }
    }
}
