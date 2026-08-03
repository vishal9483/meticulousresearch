using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// In-memory <see cref="IProjectService"/> for window-free view-model tests. Tracks call counts
/// so tests can assert that "nothing was created/deleted" without a real database.
/// </summary>
internal sealed class FakeProjectService : IProjectService
{
    private readonly List<Project> _projects = new();
    private readonly Dictionary<string, ProjectDashboard> _dashboards = new();
    private int _seq;

    public IReadOnlyList<Project> Projects => _projects;
    public int CreateCount { get; private set; }
    public int DeleteCount { get; private set; }

    public Project Create(
        string name,
        string? description = null,
        string? customInstructions = null,
        string? defaultModel = null,
        string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        CreateCount++;
        var project = new Project
        {
            Id = $"P{++_seq}",
            Name = name.Trim(),
            Description = description,
            CustomInstructions = customInstructions,
            DefaultModel = defaultModel ?? "claude-opus-5",
            Color = color,
            Archived = false,
            CreatedAt = "2026-08-03T12:00:00Z",
            UpdatedAt = "2026-08-03T12:00:00Z",
        };
        _projects.Add(project);
        return project;
    }

    public Project Rename(string projectId, string newName)
    {
        var p = Require(projectId);
        p.Name = newName.Trim();
        return p;
    }

    public Project Duplicate(string projectId, string newName)
    {
        var src = Require(projectId);
        return Create(newName, src.Description, src.CustomInstructions, src.DefaultModel, src.Color);
    }

    public Project Archive(string projectId)
    {
        var p = Require(projectId);
        p.Archived = true;
        return p;
    }

    public Project Unarchive(string projectId)
    {
        var p = Require(projectId);
        p.Archived = false;
        return p;
    }

    public void Delete(string projectId)
    {
        DeleteCount++;
        _projects.RemoveAll(p => p.Id == projectId);
    }

    public Project? Get(string projectId) => _projects.FirstOrDefault(p => p.Id == projectId);

    public IReadOnlyList<Project> List(bool includeArchived = false) =>
        _projects.Where(p => includeArchived || !p.Archived).ToList();

    public IReadOnlyList<Project> Search(string query) =>
        _projects.Where(p => !p.Archived
            && (p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (p.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToList();

    public ProjectDashboard GetDashboard(string projectId) =>
        _dashboards.TryGetValue(projectId, out var d)
            ? d
            : new ProjectDashboard(projectId, 0, 0, 0, null);

    public string BuildSystemPromptContext(string projectId) =>
        Get(projectId)?.CustomInstructions?.Trim() ?? "";

    /// <summary>Test seam: preload dashboard figures for a project.</summary>
    public void SetDashboard(ProjectDashboard dashboard) => _dashboards[dashboard.ProjectId] = dashboard;

    private Project Require(string projectId) =>
        Get(projectId) ?? throw new InvalidOperationException($"Project '{projectId}' not found.");
}
