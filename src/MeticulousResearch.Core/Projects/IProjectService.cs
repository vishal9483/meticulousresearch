using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Projects;

/// <summary>
/// The project domain service (SPEC §3.1) — the full lifecycle of research projects: create,
/// rename, duplicate, archive/unarchive, delete, read, list, search, and dashboard aggregation.
/// Owns the project as "the unit of work" (SPEC §1.3). Timestamps are taken from the injected
/// clock; the on-disk <c>projects/{id}</c> layout is managed via <c>IProjectFileStore</c>.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Creates a new (non-archived) project. <paramref name="name"/> is required; a blank name
    /// throws <see cref="ArgumentException"/> and nothing is persisted. When
    /// <paramref name="defaultModel"/> is <c>null</c> the app default model is used
    /// (<c>ISettingsService.DefaultModel</c>). Sets <c>created_at</c>/<c>updated_at</c>.
    /// </summary>
    Project Create(
        string name,
        string? description = null,
        string? customInstructions = null,
        string? defaultModel = null,
        string? color = null);

    /// <summary>Renames a project and bumps its <c>updated_at</c>.</summary>
    Project Rename(string projectId, string newName);

    /// <summary>
    /// Duplicates a project's configuration (description, custom instructions, default model,
    /// color) and its resources (rows + on-disk files) under a fresh id and the supplied name.
    /// Conversations and artifacts are NOT copied. The copy is never archived.
    /// </summary>
    Project Duplicate(string projectId, string newName);

    /// <summary>Archives a project so it is hidden from the default list.</summary>
    Project Archive(string projectId);

    /// <summary>Restores an archived project to the default list.</summary>
    Project Unarchive(string projectId);

    /// <summary>
    /// Deletes a project: removes its database rows (children cascade) and its
    /// <c>projects/{id}</c> directory on disk.
    /// </summary>
    void Delete(string projectId);

    /// <summary>Returns the project by id, or <c>null</c> when it does not exist.</summary>
    Project? Get(string projectId);

    /// <summary>
    /// Lists projects, newest-first by creation. Archived projects are excluded unless
    /// <paramref name="includeArchived"/> is <c>true</c>.
    /// </summary>
    IReadOnlyList<Project> List(bool includeArchived = false);

    /// <summary>
    /// Filters non-archived projects whose name or description contains <paramref name="query"/>
    /// (case-insensitive). A blank query returns all non-archived projects.
    /// </summary>
    IReadOnlyList<Project> Search(string query);

    /// <summary>Computes the dashboard figures (counts + last activity) for a project.</summary>
    ProjectDashboard GetDashboard(string projectId);

    /// <summary>
    /// Assembles the project's contribution to a conversation/generation system prompt — at this
    /// stage, its custom instructions (SPEC §3.1). Later features (context assembly) compose this
    /// with resource context. Returns an empty string when the project has no custom instructions.
    /// </summary>
    string BuildSystemPromptContext(string projectId);
}
