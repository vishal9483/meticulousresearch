namespace MeticulousResearch.Core.Ai.Tools;

/// <summary>
/// A single built-in tool exposed to the model loop: its stable name and a short description
/// (SPEC §7.4). The set is closed and curated — there is no registration API for users or config.
/// </summary>
/// <param name="Name">The stable tool name sent to the model.</param>
/// <param name="Description">A short human-readable description of what the tool does.</param>
public sealed record ToolDescriptor(string Name, string Description);

/// <summary>
/// The fixed, curated built-in tool set the model is given for a project generation (SPEC §7.4):
/// exactly <c>Glob</c>, <c>Grep</c>, <c>Read</c>, <c>Edit</c>, <c>Write</c>, <c>emit_artifact</c>,
/// and <c>update_artifact</c> — and nothing else. This is not a user-extensible tool/MCP
/// marketplace; the list is closed.
/// </summary>
public static class BuiltInToolSet
{
    /// <summary>The <c>Glob</c> tool name (pattern match within the project sandbox).</summary>
    public const string Glob = "Glob";

    /// <summary>The <c>Grep</c> tool name (content search across resource text and artifact versions).</summary>
    public const string Grep = "Grep";

    /// <summary>The <c>Read</c> tool name (resource extracted text, or an image as a vision block).</summary>
    public const string Read = "Read";

    /// <summary>The <c>Edit</c> tool name (revises an artifact through the artifact service).</summary>
    public const string Edit = "Edit";

    /// <summary>The <c>Write</c> tool name (authors a new artifact through the artifact service).</summary>
    public const string Write = "Write";

    /// <summary>The <c>emit_artifact</c> tool name (structured artifact create).</summary>
    public const string EmitArtifact = "emit_artifact";

    /// <summary>The <c>update_artifact</c> tool name (structured artifact update).</summary>
    public const string UpdateArtifact = "update_artifact";

    /// <summary>The exact, ordered set of tool descriptors exposed to the model — and nothing else.</summary>
    public static IReadOnlyList<ToolDescriptor> Tools { get; } = new[]
    {
        new ToolDescriptor(Glob, "Find files by glob pattern within the project sandbox."),
        new ToolDescriptor(Grep, "Search content across resource text and artifact versions."),
        new ToolDescriptor(Read, "Read a resource's extracted text, or an image as a vision content block."),
        new ToolDescriptor(Edit, "Revise an existing artifact, creating a new version."),
        new ToolDescriptor(Write, "Author a new artifact (never overwrites a file)."),
        new ToolDescriptor(EmitArtifact, "Create a new artifact from a structured request."),
        new ToolDescriptor(UpdateArtifact, "Record a new version of an existing artifact from a structured request."),
    };

    /// <summary>The exact, ordered set of tool names exposed to the model.</summary>
    public static IReadOnlyList<string> ToolNames { get; } =
        Tools.Select(t => t.Name).ToArray();

    /// <summary>Returns whether a tool name belongs to the curated set (case-sensitive).</summary>
    public static bool Contains(string name) => ToolNames.Contains(name, StringComparer.Ordinal);
}
