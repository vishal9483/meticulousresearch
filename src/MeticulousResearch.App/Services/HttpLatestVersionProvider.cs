using System.Net.Http;
using MeticulousResearch.Core.Updates;

namespace MeticulousResearch.App.Services;

/// <summary>
/// The thin network adapter behind <see cref="ILatestVersionProvider"/> (update-notice/phase.md,
/// SPEC §8). It fetches the latest advertised version string from a configured update source over
/// HTTPS and returns it verbatim; all comparison and dismissal logic stays in
/// <see cref="UpdateService"/>. When no update source is configured it simply returns <c>null</c>
/// (no update). Network egress is limited to the configured update source (SPEC §7.5); failures
/// propagate to the service, which swallows them to "no notice".
/// </summary>
public sealed class HttpLatestVersionProvider : ILatestVersionProvider
{
    private readonly HttpClient _httpClient;
    private readonly Uri? _updateSourceUri;

    /// <summary>Creates the adapter over an <see cref="HttpClient"/> and an optional update-source URI.</summary>
    /// <param name="httpClient">The shared HTTP client.</param>
    /// <param name="updateSourceUri">The update source that advertises the latest version, or <c>null</c> when unconfigured.</param>
    public HttpLatestVersionProvider(HttpClient httpClient, Uri? updateSourceUri)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _updateSourceUri = updateSourceUri;
    }

    /// <inheritdoc />
    public async Task<string?> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        if (_updateSourceUri is null)
            return null;

        var body = await _httpClient.GetStringAsync(_updateSourceUri, cancellationToken).ConfigureAwait(false);
        return body?.Trim();
    }
}
