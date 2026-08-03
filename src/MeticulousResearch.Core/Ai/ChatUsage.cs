namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The token-usage figures a backend surfaces on completion (SPEC §3.6). Both the sidecar and the
/// direct-API backends map the API's <c>usage</c> object onto these four fields so cost/usage
/// metering works identically regardless of which backend answered. The cache fields default to
/// <c>0</c> when the API omits them, never an error.
/// </summary>
/// <param name="InputTokens">Billed input (prompt) tokens.</param>
/// <param name="OutputTokens">Billed output (completion) tokens.</param>
/// <param name="CacheReadTokens">Prompt-cache read tokens (0 when absent).</param>
/// <param name="CacheWriteTokens">Prompt-cache write tokens (0 when absent).</param>
public sealed record ChatUsage(
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens = 0,
    long CacheWriteTokens = 0)
{
    /// <summary>All-zero usage — the neutral value before any tokens are reported.</summary>
    public static ChatUsage Zero { get; } = new(0, 0, 0, 0);
}
