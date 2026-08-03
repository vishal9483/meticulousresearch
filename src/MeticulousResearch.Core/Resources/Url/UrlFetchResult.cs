namespace MeticulousResearch.Core.Resources.Url;

/// <summary>
/// The transport-level outcome of an <see cref="IUrlFetcher"/> fetch (SPEC §3.2, §3.7). Distinguishes
/// a successful response from the failure modes the URL resource flow must surface as actionable
/// errors without creating a resource.
/// </summary>
public enum UrlFetchOutcome
{
    /// <summary>A response was received with a success (2xx) status.</summary>
    Ok,

    /// <summary>The host could not be reached (DNS/socket failure, no response).</summary>
    ConnectionError,

    /// <summary>The request exceeded its time budget before a response arrived.</summary>
    Timeout,

    /// <summary>A response was received but with a non-success HTTP status (e.g. 404, 500).</summary>
    HttpError,
}

/// <summary>
/// The result of fetching a URL: the transport <see cref="Outcome"/>, the HTTP status code (when a
/// response arrived), the response content type, and the raw body. Keeping all network behind this
/// value lets the URL resource flow decide convert-vs-error deterministically and offline in tests.
/// </summary>
public sealed class UrlFetchResult
{
    /// <summary>Creates a fetch result.</summary>
    public UrlFetchResult(UrlFetchOutcome outcome, int? statusCode, string? contentType, string? body)
    {
        Outcome = outcome;
        StatusCode = statusCode;
        ContentType = contentType;
        Body = body;
    }

    /// <summary>The transport-level outcome.</summary>
    public UrlFetchOutcome Outcome { get; }

    /// <summary>The HTTP status code when a response arrived; null for connection/timeout errors.</summary>
    public int? StatusCode { get; }

    /// <summary>The response's content type (e.g. <c>text/html</c>); null when unavailable.</summary>
    public string? ContentType { get; }

    /// <summary>The raw response body (HTML) on success; null when no response arrived.</summary>
    public string? Body { get; }

    /// <summary>Creates a successful HTML result with status 200.</summary>
    public static UrlFetchResult Success(string body, string contentType = "text/html")
        => new(UrlFetchOutcome.Ok, 200, contentType, body);
}
