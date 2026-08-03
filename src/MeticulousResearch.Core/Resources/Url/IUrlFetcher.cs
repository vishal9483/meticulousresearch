namespace MeticulousResearch.Core.Resources.Url;

/// <summary>
/// Performs the HTTP fetch for a URL resource (SPEC §3.2). Abstracted so the conversion/storage flow
/// stays deterministic and offline in unit tests: tests inject a fake with scripted responses
/// (bodies, titles, 404/500/timeout/connection-error) while production uses an HTTP-backed fetcher.
/// </summary>
public interface IUrlFetcher
{
    /// <summary>
    /// Fetches the given absolute <paramref name="url"/> and returns the transport outcome plus the
    /// raw body/content-type on success. Never throws for expected network failures — those are
    /// reported via <see cref="UrlFetchResult.Outcome"/>.
    /// </summary>
    UrlFetchResult Fetch(string url);
}
