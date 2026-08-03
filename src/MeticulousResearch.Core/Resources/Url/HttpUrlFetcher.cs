using System.Net.Http;

namespace MeticulousResearch.Core.Resources.Url;

/// <summary>
/// HTTP-backed <see cref="IUrlFetcher"/> (SPEC §3.2). Wraps an <see cref="HttpClient"/> whose
/// <see cref="HttpMessageHandler"/> is injectable so tests can script responses and failures without
/// touching the network. Expected network failures are mapped to <see cref="UrlFetchOutcome"/> rather
/// than thrown, so the resource flow can surface actionable errors.
/// </summary>
public sealed class HttpUrlFetcher : IUrlFetcher
{
    private readonly HttpClient _client;

    /// <summary>Creates a fetcher over an existing <see cref="HttpClient"/>.</summary>
    public HttpUrlFetcher(HttpClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>Creates a fetcher over a given message handler (used to inject fakes in tests).</summary>
    public HttpUrlFetcher(HttpMessageHandler handler)
        : this(new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler))))
    {
    }

    /// <summary>Creates a fetcher over a default <see cref="HttpClient"/>.</summary>
    public static HttpUrlFetcher CreateDefault() => new(new HttpClient());

    /// <inheritdoc />
    public UrlFetchResult Fetch(string url)
    {
        try
        {
            using var response = _client.Send(new HttpRequestMessage(HttpMethod.Get, url));
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var status = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? new UrlFetchResult(UrlFetchOutcome.Ok, status, contentType, body)
                : new UrlFetchResult(UrlFetchOutcome.HttpError, status, contentType, body);
        }
        catch (TaskCanceledException)
        {
            return new UrlFetchResult(UrlFetchOutcome.Timeout, null, null, null);
        }
        catch (OperationCanceledException)
        {
            return new UrlFetchResult(UrlFetchOutcome.Timeout, null, null, null);
        }
        catch (HttpRequestException)
        {
            return new UrlFetchResult(UrlFetchOutcome.ConnectionError, null, null, null);
        }
    }
}
