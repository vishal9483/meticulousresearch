namespace MeticulousResearch.Core.Search;

/// <summary>The kind of content a <see cref="SearchHit"/> came from (SPEC §3.1).</summary>
public enum SearchContentType
{
    /// <summary>A project resource's extracted text.</summary>
    Resource,

    /// <summary>A conversation message's content (wired by M2 <c>conversations</c>).</summary>
    Message,

    /// <summary>An artifact version's content (wired by M3 <c>artifacts</c>).</summary>
    Artifact,
}

/// <summary>
/// A single ranked full-text-search match within a project (SPEC §3.1). Lightweight by design so a
/// view-model can render a result list without loading the underlying blob: the owning content's
/// id and title plus an optional highlighted snippet.
/// </summary>
/// <param name="ContentType">Which content type the match came from.</param>
/// <param name="Id">The id of the matched content row (resource / message / artifact-version id).</param>
/// <param name="Title">A human-readable title for the match (resource / conversation / artifact title).</param>
/// <param name="Snippet">An optional excerpt of the matched text, or <c>null</c> when unavailable.</param>
public sealed record SearchHit(SearchContentType ContentType, string Id, string Title, string? Snippet);
