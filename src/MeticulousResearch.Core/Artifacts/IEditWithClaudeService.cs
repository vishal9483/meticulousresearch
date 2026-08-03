using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Artifacts;

/// <summary>
/// The "Edit with Claude" and manual-edit iteration engine (SPEC §3.4, §5). Refines an existing
/// artifact by giving Claude a follow-up instruction that produces a <em>new</em> version, and lets a
/// manual content edit create a version too. Both paths funnel through the single version-creation
/// entry point (<see cref="IArtifactService.AddVersion"/>) owned by <c>artifact-versioning</c> — this
/// feature never rewrites history, so immutability, ordering, and provenance stay in one place. Owned
/// by <c>edit-with-claude</c> (M3); pairs with <c>artifact-diff</c> (review before keeping) and is
/// read by <c>cost-tracking</c> (M4), which consumes the usage recorded on Claude-authored versions.
/// </summary>
public interface IEditWithClaudeService
{
    /// <summary>
    /// Refines the artifact with a follow-up <paramref name="instruction"/> through
    /// <see cref="IChatService"/>: assembles the request from the project's custom instructions, its
    /// <em>enabled</em> resources, and the current version's content (so Claude revises rather than
    /// regenerates), streams the revised content into <paramref name="preview"/>, and — only when the
    /// stream completes successfully — commits a new Claude-authored version recording the model, the
    /// instruction as its prompt, the in-scope resource ids, and the token usage/cost. Cancel and
    /// failure commit nothing and leave the current version intact.
    /// </summary>
    /// <param name="artifactId">The artifact being edited.</param>
    /// <param name="instruction">The follow-up instruction (must be non-empty).</param>
    /// <param name="model">The model to run this edit with (chosen per edit).</param>
    /// <param name="preview">Receives the cumulative revised content as it streams (optional).</param>
    /// <param name="cancellationToken">Cancels the in-progress edit; no version is committed.</param>
    /// <returns>The newly-committed, now-current Claude-authored version.</returns>
    /// <exception cref="ArtifactValidationException">The instruction or model is empty.</exception>
    /// <exception cref="InvalidOperationException">The artifact does not exist or the edit failed.</exception>
    /// <exception cref="OperationCanceledException">The edit was cancelled before completion.</exception>
    Task<ArtifactVersion> EditWithClaude(
        string artifactId,
        string instruction,
        string model,
        IProgress<string>? preview = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a manual edit: commits a new user-authored version (usage/cost 0) when
    /// <paramref name="content"/> differs from the current version, or does nothing and returns
    /// <c>null</c> when the content is unchanged (a no-op save creates no version).
    /// </summary>
    /// <param name="artifactId">The artifact being edited.</param>
    /// <param name="content">The edited content to save.</param>
    /// <returns>The new user-authored version, or <c>null</c> when the content was unchanged.</returns>
    /// <exception cref="InvalidOperationException">The artifact does not exist.</exception>
    ArtifactVersion? SaveManualEdit(string artifactId, string content);
}
