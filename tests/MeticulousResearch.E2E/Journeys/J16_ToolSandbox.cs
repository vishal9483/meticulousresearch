using System.Linq;
using MeticulousResearch.Core.Ai.Tools;
using MeticulousResearch.Core.Resources.Vision;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-16 — Built-in tool sandbox transparency &amp; confinement (covers SPEC §7.4). Tool calls made
/// during generation are logged and visible, a Write lands as a new artifact version (never a silent
/// overwrite), and every path-bearing tool call is confined to the active project's sandbox.
/// </summary>
public sealed class J16_ToolSandbox : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;
    private readonly ToolCallLog _log = new();

    public J16_ToolSandbox() => _projectId = _h.Projects.Create("EV Market 2026").Id;

    public void Dispose() => _h.Dispose();

    private ProjectToolInvoker NewInvoker()
    {
        var sandbox = new ProjectSandbox(_h.Store.FileStore.GetProjectDirectory(_projectId));
        return new ProjectToolInvoker(
            _projectId, sandbox, _h.Store, _h.Resources, new VisionContentAssembler(), _h.Artifacts, _log);
    }

    // @e2e
    // Scenario: Tool calls during generation are visible in the conversation
    [Fact]
    public void Tool_calls_during_generation_are_visible_and_writes_land_as_artifact_versions()
    {
        _h.Resources.AddText(_projectId, "Filing", "The market grows through 2030.");
        var invoker = NewInvoker();

        // When the turn runs (the model calls Grep then Write).
        invoker.Grep("market");
        var write = invoker.Write("Summary", "# Summary\nThe market grows.");

        // Then each tool call is logged and shown in the conversation for transparency.
        Assert.Contains(_log.Calls, c => c.Tool == "Grep");
        Assert.Contains(_log.Calls, c => c.Tool == "Write");

        // And the Write tool call lands as a new artifact version (never a silent overwrite).
        Assert.NotNull(write);
        Assert.NotEmpty(_h.Artifacts.List(_projectId));
    }

    // @e2e @unit
    // Scenario Outline: Tools cannot escape the active project's sandbox (rejected paths)
    [Theory]
    [InlineData("../other-project/db.sqlite")]
    [InlineData("../../Windows/System32/hosts")]
    [InlineData("../../../etc/passwd")]
    public void Tools_cannot_escape_the_active_projects_sandbox(string escapePath)
    {
        var invoker = NewInvoker();
        Assert.Throws<SandboxViolationException>(() => invoker.Read(escapePath));
    }

    // @e2e @unit — the shared database file (outside any project subtree) is likewise rejected.
    [Fact]
    public void Reading_the_shared_database_file_is_rejected()
    {
        var invoker = NewInvoker();
        Assert.Throws<SandboxViolationException>(() => invoker.Read(_h.Store.DatabasePath));
    }

    // @e2e @unit — an in-sandbox path is allowed.
    [Fact]
    public void An_in_sandbox_path_is_allowed()
    {
        var projectDir = _h.Store.FileStore.GetProjectDirectory(_projectId);
        var relative = Path.Combine("resources", "note.txt");
        Directory.CreateDirectory(Path.Combine(projectDir, "resources"));
        File.WriteAllText(Path.Combine(projectDir, relative), "in-sandbox content");

        var result = NewInvoker().Read(relative);
        Assert.NotNull(result);
    }
}
