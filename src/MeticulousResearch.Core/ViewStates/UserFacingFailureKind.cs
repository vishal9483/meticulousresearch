namespace MeticulousResearch.Core.ViewStates;

/// <summary>
/// The classification of a failure as surfaced to the user (SPEC §3.7). Stays aligned with the
/// gateway/settings error taxonomy (401→missing key, network→offline, 429→rate limited) plus the
/// file-extraction failure, with an <see cref="Unexpected"/> catch-all for anything unclassified.
/// </summary>
public enum UserFacingFailureKind
{
    /// <summary>No API key is configured; recovery is to open Settings.</summary>
    MissingApiKey,

    /// <summary>The machine appears to be offline; recovery is to retry.</summary>
    Offline,

    /// <summary>The API returned 429; the app is retrying and the user may retry.</summary>
    RateLimited,

    /// <summary>A resource's text could not be extracted; recovery is to re-extract.</summary>
    ExtractionFailed,

    /// <summary>An unclassified failure; the user sees a generic message and the detail is logged.</summary>
    Unexpected,
}
