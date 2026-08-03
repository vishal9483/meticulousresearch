using System.Text.Json;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Turns;

/// <summary>
/// The per-turn metadata surfaced on a completed assistant turn (SPEC §3.3): the model that produced
/// it, the token usage (input/output plus prompt-cache read/write), the end-to-end latency, and the
/// resource ids that were in scope. Projected from the persisted <see cref="Message"/> fields so the
/// turn-metadata view and the per-turn cost badge (<c>turn-metadata-actions</c>) read a stable
/// snapshot without re-querying the backend.
/// </summary>
public sealed record TurnMetadata
{
    /// <summary>The model id that produced the turn, or <c>null</c> when unrecorded.</summary>
    public string? Model { get; init; }

    /// <summary>Billed input (prompt) tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Billed output (completion) tokens.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Prompt-cache read tokens.</summary>
    public long CacheReadTokens { get; init; }

    /// <summary>Prompt-cache write tokens.</summary>
    public long CacheWriteTokens { get; init; }

    /// <summary>End-to-end latency in milliseconds, or <c>null</c> when unrecorded.</summary>
    public long? LatencyMs { get; init; }

    /// <summary>The resource ids that were in scope for the turn (empty when none/unrecorded).</summary>
    public IReadOnlyList<string> ResourceScope { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Projects the metadata from a persisted assistant <see cref="Message"/>, parsing the JSON
    /// array of in-scope resource ids (a malformed/blank scope yields an empty list, never an error).
    /// </summary>
    /// <param name="message">The persisted assistant message row.</param>
    /// <exception cref="ArgumentNullException">The message is null.</exception>
    public static TurnMetadata FromMessage(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new TurnMetadata
        {
            Model = message.Model,
            InputTokens = message.TokensIn,
            OutputTokens = message.TokensOut,
            CacheReadTokens = message.TokensCacheRead,
            CacheWriteTokens = message.TokensCacheWrite,
            LatencyMs = message.LatencyMs,
            ResourceScope = ParseScope(message.ResourceScopeJson),
        };
    }

    private static IReadOnlyList<string> ParseScope(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
