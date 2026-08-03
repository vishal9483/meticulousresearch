using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Turns;

namespace MeticulousResearch.Core.Artifacts;

/// <summary>
/// <see cref="IEditWithClaudeService"/> over the shared M3 contracts (SPEC §3.4, §5). Reuses the
/// ai-gateway generation contract (<see cref="IChatService"/>) for the Claude call and the
/// artifact-versioning entry point (<see cref="IArtifactService.AddVersion"/>) for every committed
/// version, so this feature adds no new persistence and never rewrites history. The follow-up
/// request carries the current version's content as the edit target and grounds only the project's
/// <em>enabled</em> resources, consistent with deliverable-templates.
/// </summary>
public sealed class EditWithClaudeService : IEditWithClaudeService
{
    private readonly IArtifactService _artifacts;
    private readonly IChatService _chat;
    private readonly IProjectService _projects;
    private readonly IResourceService _resources;
    private readonly ITurnCostCalculator? _costCalculator;

    /// <summary>Creates the edit service over its collaborators.</summary>
    /// <param name="artifacts">The versioning entry point every committed version routes through.</param>
    /// <param name="chat">The generation gateway used for the Claude edit.</param>
    /// <param name="projects">Supplies the project's custom instructions as system context.</param>
    /// <param name="resources">Supplies the project's enabled resources for grounding.</param>
    /// <param name="costCalculator">
    /// Optional per-turn cost seam used to price a Claude edit's <c>cost_usd</c> from its usage
    /// (SPEC §3.6). When null, Claude-authored versions record no cost. The authoritative engine is
    /// owned by <c>cost-tracking</c> (M4).
    /// </param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    public EditWithClaudeService(
        IArtifactService artifacts,
        IChatService chat,
        IProjectService projects,
        IResourceService resources,
        ITurnCostCalculator? costCalculator = null)
    {
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _costCalculator = costCalculator;
    }

    /// <inheritdoc />
    public async Task<ArtifactVersion> EditWithClaude(
        string artifactId,
        string instruction,
        string model,
        IProgress<string>? preview = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            throw new ArtifactValidationException("A follow-up instruction is required to edit with Claude.");
        if (string.IsNullOrWhiteSpace(model))
            throw new ArtifactValidationException("A model is required to edit with Claude.");

        var artifact = _artifacts.Get(artifactId)
            ?? throw new InvalidOperationException($"Artifact '{artifactId}' does not exist.");
        var currentContent = CurrentContent(artifact);

        var project = _projects.Get(artifact.ProjectId);
        var scope = BuildEnabledScope(artifact.ProjectId);

        var context = new ChatAskContext
        {
            Model = model,
            UserMessage = BuildEditMessage(instruction, currentContent),
            CustomInstructions = project?.CustomInstructions,
            Resources = scope,
        };

        string? content = null;
        ChatUsage usage = ChatUsage.Zero;
        var accumulated = new System.Text.StringBuilder();
        await foreach (var evt in _chat.Ask(context, cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                case ChatTokenDelta delta:
                    accumulated.Append(delta.Text);
                    preview?.Report(accumulated.ToString());
                    break;
                case ChatCompleted completed:
                    content = completed.Text;
                    usage = completed.Usage;
                    break;
                case ChatCancelled:
                    throw new OperationCanceledException("The Claude edit was cancelled.");
                case ChatFaulted faulted:
                    throw new InvalidOperationException(faulted.Message);
            }
        }

        if (content is null)
            throw new InvalidOperationException("The Claude edit produced no completion.");

        var cost = PriceEdit(model, usage);
        var provenance = ArtifactProvenance.Claude(
            model,
            instruction,
            scope.Select(r => r.Id).ToArray(),
            usage.InputTokens,
            usage.OutputTokens,
            cost);
        return _artifacts.AddVersion(artifactId, content, provenance);
    }

    /// <inheritdoc />
    public ArtifactVersion? SaveManualEdit(string artifactId, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var artifact = _artifacts.Get(artifactId)
            ?? throw new InvalidOperationException($"Artifact '{artifactId}' does not exist.");
        var currentContent = CurrentContent(artifact);

        if (Normalize(content) == Normalize(currentContent))
            return null;

        return _artifacts.AddVersion(artifactId, content, ArtifactProvenance.User());
    }

    private string CurrentContent(Artifact artifact)
    {
        var history = _artifacts.GetHistory(artifact.Id);
        var current = history.FirstOrDefault(v => v.Id == artifact.CurrentVersionId)
            ?? history.OrderByDescending(v => v.VersionNo).FirstOrDefault();
        return current?.Content ?? "";
    }

    private IReadOnlyList<ChatResource> BuildEnabledScope(string projectId)
    {
        return _resources.ListEnabled(projectId)
            .Select(r => new ChatResource(r.Id, r.Title, _resources.GetExtractedText(r.Id)))
            .ToList();
    }

    private double? PriceEdit(string model, ChatUsage usage)
    {
        if (_costCalculator is null)
            return null;

        return _costCalculator.Calculate(new TurnMetadata
        {
            Model = model,
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
            CacheReadTokens = usage.CacheReadTokens,
            CacheWriteTokens = usage.CacheWriteTokens,
        }).Total;
    }

    /// <summary>
    /// Builds the follow-up request body so Claude revises the current artifact rather than
    /// regenerating from scratch: the instruction plus the current version's content as the edit
    /// target (SPEC §3.4).
    /// </summary>
    private static string BuildEditMessage(string instruction, string currentContent) =>
        "You are revising an existing artifact. Apply the following instruction to the current " +
        "content and return the full revised artifact.\n\n" +
        $"Instruction:\n{instruction}\n\n" +
        $"Current artifact content:\n{currentContent}";

    private static string Normalize(string content) =>
        content.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
}
