namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The stable prompt segment a <see cref="CacheBreakpoint"/> terminates (prompt-caching, SPEC §8).
/// Only the two stable, cache-worthy segments are modelled — the system prompt (the project's custom
/// instructions) and the enabled-resource grounding context. The volatile tail (recent history and
/// the new user message) is never a cache segment.
/// </summary>
public enum ChatCacheSegment
{
    /// <summary>The system prompt segment (the project's custom instructions).</summary>
    System,

    /// <summary>The stable enabled-resource grounding context segment.</summary>
    Resources,
}

/// <summary>
/// A backend-agnostic marker that the <see cref="ChatRequestAssembler"/> places at the end of a
/// stable prompt segment so both backends can reuse cached input across turns and regenerations
/// (prompt-caching, SPEC §8). Each backend translates the placement decision into its own
/// cache-control mechanism (the direct-API <c>cache_control</c> block marker; the Agent SDK sidecar's
/// prompt-caching helpers). <see cref="CacheKey"/> is a stable digest of the segment's exact content,
/// so an unchanged segment reuses the cache across turns while any change (e.g. an altered resource
/// scope) yields a different key and invalidates the stale segment — never a false cache hit.
/// </summary>
/// <param name="Segment">The stable segment this breakpoint terminates.</param>
/// <param name="CacheKey">A stable content digest identifying the segment for reuse/invalidation.</param>
public sealed record CacheBreakpoint(ChatCacheSegment Segment, string CacheKey);
