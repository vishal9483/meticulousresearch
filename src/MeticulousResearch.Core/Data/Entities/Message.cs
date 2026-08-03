namespace MeticulousResearch.Core.Data.Entities;

/// <summary>
/// A single turn in a conversation. Token and cost columns are snapshotted at turn completion
/// so historical cost is stable even if pricing changes. Maps to the <c>Message</c> table (SPEC §5).
/// </summary>
public sealed class Message
{
    /// <summary>Stable message identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Owning conversation id (FK to <see cref="Conversation"/>).</summary>
    public string ConversationId { get; set; } = "";

    /// <summary>Role: <c>system | user | assistant</c>.</summary>
    public string Role { get; set; } = "";

    /// <summary>Message text content.</summary>
    public string Content { get; set; } = "";

    /// <summary>Model id used for this turn (nullable for user/system rows).</summary>
    public string? Model { get; set; }

    /// <summary>Input tokens billed for this turn.</summary>
    public long TokensIn { get; set; }

    /// <summary>Output tokens billed for this turn.</summary>
    public long TokensOut { get; set; }

    /// <summary>Cache-read tokens (prompt caching) billed for this turn.</summary>
    public long TokensCacheRead { get; set; }

    /// <summary>Cache-write tokens (prompt caching) billed for this turn.</summary>
    public long TokensCacheWrite { get; set; }

    /// <summary>Snapshot USD cost computed at turn completion (nullable until known).</summary>
    public double? CostUsd { get; set; }

    /// <summary>End-to-end latency in milliseconds (nullable).</summary>
    public long? LatencyMs { get; set; }

    /// <summary>JSON array of resource ids that were in scope for this turn (nullable).</summary>
    public string? ResourceScopeJson { get; set; }

    /// <summary>UTC creation instant (ISO-8601).</summary>
    public string CreatedAt { get; set; } = "";
}
