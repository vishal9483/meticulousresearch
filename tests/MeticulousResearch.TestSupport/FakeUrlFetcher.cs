using MeticulousResearch.Core.Resources.Url;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// A scriptable <see cref="IUrlFetcher"/> test double: maps URLs to canned <see cref="UrlFetchResult"/>
/// responses so URL-resource tests stay deterministic and offline (no real network). Unmapped URLs
/// yield a connection error by default.
/// </summary>
public sealed class FakeUrlFetcher : IUrlFetcher
{
    private readonly Dictionary<string, UrlFetchResult> _responses = new(StringComparer.Ordinal);

    /// <summary>The number of times <see cref="Fetch"/> has been called (proves no re-fetch).</summary>
    public int FetchCount { get; private set; }

    /// <summary>Scripts an HTML success response (status 200) for <paramref name="url"/>.</summary>
    public FakeUrlFetcher WithHtml(string url, string html)
    {
        _responses[url] = UrlFetchResult.Success(html);
        return this;
    }

    /// <summary>Scripts an arbitrary result for <paramref name="url"/>.</summary>
    public FakeUrlFetcher WithResult(string url, UrlFetchResult result)
    {
        _responses[url] = result;
        return this;
    }

    /// <inheritdoc />
    public UrlFetchResult Fetch(string url)
    {
        FetchCount++;
        return _responses.TryGetValue(url, out var result)
            ? result
            : new UrlFetchResult(UrlFetchOutcome.ConnectionError, null, null, null);
    }
}
