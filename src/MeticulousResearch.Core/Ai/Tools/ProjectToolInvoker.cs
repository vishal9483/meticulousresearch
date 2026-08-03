using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Vision;

namespace MeticulousResearch.Core.Ai.Tools;

/// <summary>
/// Executes the curated built-in tools for a single project generation, confined to the project's
/// sandbox and logging every call for transparency (SPEC §7.4, §3.2.1, §3.4). Read/search tools
/// (<c>Glob</c>, <c>Grep</c>, <c>Read</c>) operate over the project's resources and artifact
/// versions; authoring tools (<c>Edit</c>, <c>Write</c>, <c>emit_artifact</c>,
/// <c>update_artifact</c>) route exclusively through <see cref="IArtifactService"/> so results land
/// as new artifact versions rather than silently overwriting files. Path-bearing tool calls are
/// validated by the <see cref="ProjectSandbox"/> before any filesystem access.
/// </summary>
public sealed class ProjectToolInvoker
{
    private readonly string _projectId;
    private readonly ProjectSandbox _sandbox;
    private readonly DataStore _store;
    private readonly IResourceService _resources;
    private readonly VisionContentAssembler _vision;
    private readonly IArtifactService _artifacts;
    private readonly ToolCallLog _log;

    private const int SnippetRadius = 40;

    /// <summary>Creates an invoker for the given project.</summary>
    /// <param name="projectId">The active project id (its sandbox is <c>projects/{projectId}</c>).</param>
    /// <param name="sandbox">The sandbox guard confining every path-bearing tool call.</param>
    /// <param name="store">The data store used to search resources and artifact versions.</param>
    /// <param name="resources">The resource service used to read extracted text and images.</param>
    /// <param name="vision">The shared vision-content assembler used for image reads.</param>
    /// <param name="artifacts">The artifact service that authoring tools route through.</param>
    /// <param name="log">The per-turn tool-call log every call is recorded to.</param>
    public ProjectToolInvoker(
        string projectId,
        ProjectSandbox sandbox,
        DataStore store,
        IResourceService resources,
        VisionContentAssembler vision,
        IArtifactService artifacts,
        ToolCallLog log)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project id is required.", nameof(projectId));
        _projectId = projectId;
        _sandbox = sandbox ?? throw new ArgumentNullException(nameof(sandbox));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _vision = vision ?? throw new ArgumentNullException(nameof(vision));
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The project's sandbox root.</summary>
    public string SandboxRoot => _sandbox.Root;

    /// <summary>
    /// <c>Glob</c>: lists files under the project sandbox whose name matches <paramref name="pattern"/>,
    /// returned as project-relative paths. Only files within the sandbox are ever enumerated, so
    /// nothing outside the project can be listed.
    /// </summary>
    public IReadOnlyList<string> Glob(string pattern)
    {
        var regex = GlobToRegex(pattern ?? string.Empty);
        var results = new List<string>();
        if (Directory.Exists(_sandbox.Root))
        {
            foreach (var path in Directory.EnumerateFiles(_sandbox.Root, "*", SearchOption.AllDirectories))
            {
                if (regex.IsMatch(Path.GetFileName(path)))
                    results.Add(Path.GetRelativePath(_sandbox.Root, path).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        results.Sort(StringComparer.Ordinal);
        Record(BuiltInToolSet.Glob, new[] { Kv("pattern", pattern ?? string.Empty) },
            $"{results.Count} file(s) matched", success: true);
        return results;
    }

    /// <summary>
    /// <c>Grep</c>: searches for <paramref name="query"/> across the project's resource extracted text
    /// and artifact-version content, returning each hit with a short snippet.
    /// </summary>
    public IReadOnlyList<GrepMatch> Grep(string query)
    {
        var matches = new List<GrepMatch>();
        var term = query ?? string.Empty;
        if (term.Length > 0)
        {
            using var db = _store.CreateDbContext();

            var resources = db.Resources.AsNoTracking()
                .Where(r => r.ProjectId == _projectId)
                .ToList();
            foreach (var r in resources)
            {
                var text = r.ExtractedText ?? string.Empty;
                var idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                    matches.Add(new GrepMatch("resource", r.Id, r.Title, Snippet(text, idx, term.Length)));
            }

            var artifactIds = db.Artifacts.AsNoTracking()
                .Where(a => a.ProjectId == _projectId)
                .Select(a => a.Id)
                .ToList();
            var versions = db.ArtifactVersions.AsNoTracking()
                .Where(v => artifactIds.Contains(v.ArtifactId))
                .ToList();
            foreach (var v in versions)
            {
                var content = v.Content ?? string.Empty;
                var idx = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                    matches.Add(new GrepMatch("artifact", v.Id, v.ArtifactId, Snippet(content, idx, term.Length)));
            }
        }

        Record(BuiltInToolSet.Grep, new[] { Kv("query", term) },
            $"{matches.Count} match(es)", success: true);
        return matches;
    }

    /// <summary>
    /// <c>Read</c>: returns a resource's extracted text, or — for an image resource — an
    /// <see cref="ImageContentBlock"/> vision block (SPEC §3.2.1), never raw bytes as text. When the
    /// target is not a known resource it is treated as a project-relative file path and validated by
    /// the sandbox guard before any file is read.
    /// </summary>
    /// <exception cref="SandboxViolationException">A path target escapes the project sandbox.</exception>
    public ToolReadResult Read(string target)
    {
        var resource = string.IsNullOrWhiteSpace(target) ? null : _resources.Get(target);
        if (resource is not null && resource.ProjectId == _projectId)
        {
            if (resource.Type == ResourceTypes.Image)
            {
                var block = _vision.Assemble(resource);
                Record(BuiltInToolSet.Read, new[] { Kv("target", target) }, "image content block", success: true);
                return ToolReadResult.FromImage(block);
            }

            var text = _resources.GetExtractedText(resource.Id);
            Record(BuiltInToolSet.Read, new[] { Kv("target", target) }, $"{text.Length} char(s)", success: true);
            return ToolReadResult.FromText(text);
        }

        // Not a resource id: treat as a file path and enforce the sandbox boundary.
        string full;
        try
        {
            full = _sandbox.Resolve(target);
        }
        catch (SandboxViolationException)
        {
            Record(BuiltInToolSet.Read, new[] { Kv("target", target ?? string.Empty) },
                "rejected: sandbox violation", success: false);
            throw;
        }

        var fileText = File.Exists(full) ? File.ReadAllText(full) : string.Empty;
        Record(BuiltInToolSet.Read, new[] { Kv("target", target) }, $"{fileText.Length} char(s)", success: true);
        return ToolReadResult.FromText(fileText);
    }

    /// <summary>
    /// <c>Write</c>: authors a new artifact through <see cref="IArtifactService"/> (SPEC §7.4, §3.4).
    /// It never writes to or overwrites a file on disk — the result lands as a new artifact version.
    /// </summary>
    public ArtifactMutationResult Write(string title, string content, string kind = "document")
    {
        var result = _artifacts.EmitArtifact(new ArtifactEmitCommand(_projectId, title, kind, content));
        Record(BuiltInToolSet.Write, new[] { Kv("title", title), Kv("kind", kind) },
            $"artifact {result.ArtifactId} v{result.Version}", success: true);
        return result;
    }

    /// <summary>
    /// <c>Edit</c>: revises an existing artifact through <see cref="IArtifactService"/>, creating a
    /// new version and preserving prior ones (SPEC §7.4, §3.4).
    /// </summary>
    public ArtifactMutationResult Edit(string artifactId, string content, string? changeNote = null)
    {
        var result = _artifacts.UpdateArtifact(new ArtifactUpdateCommand(artifactId, content, changeNote));
        Record(BuiltInToolSet.Edit, new[] { Kv("artifactId", artifactId) },
            $"artifact {result.ArtifactId} v{result.Version}", success: true);
        return result;
    }

    /// <summary>
    /// <c>emit_artifact</c>: the structured artifact-create tool — maps directly to
    /// <see cref="IArtifactService.EmitArtifact"/> (SPEC §7.4).
    /// </summary>
    public ArtifactMutationResult EmitArtifact(string title, string content, string kind = "document")
    {
        var result = _artifacts.EmitArtifact(new ArtifactEmitCommand(_projectId, title, kind, content));
        Record(BuiltInToolSet.EmitArtifact, new[] { Kv("title", title), Kv("kind", kind) },
            $"artifact {result.ArtifactId} v{result.Version}", success: true);
        return result;
    }

    /// <summary>
    /// <c>update_artifact</c>: the structured artifact-update tool — maps directly to
    /// <see cref="IArtifactService.UpdateArtifact"/> (SPEC §7.4).
    /// </summary>
    public ArtifactMutationResult UpdateArtifact(string artifactId, string content, string? changeNote = null)
    {
        var result = _artifacts.UpdateArtifact(new ArtifactUpdateCommand(artifactId, content, changeNote));
        Record(BuiltInToolSet.UpdateArtifact, new[] { Kv("artifactId", artifactId) },
            $"artifact {result.ArtifactId} v{result.Version}", success: true);
        return result;
    }

    private void Record(string tool, KeyValuePair<string, string>[] inputs, string outcome, bool success) =>
        _log.Record(new ToolCallRecord(tool, inputs, outcome, success));

    private static KeyValuePair<string, string> Kv(string key, string value) => new(key, value);

    private static string Snippet(string text, int index, int length)
    {
        var start = Math.Max(0, index - SnippetRadius);
        var end = Math.Min(text.Length, index + length + SnippetRadius);
        return text.Substring(start, end - start);
    }

    private static Regex GlobToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal);
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
