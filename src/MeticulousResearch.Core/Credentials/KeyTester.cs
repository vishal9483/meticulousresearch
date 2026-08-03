using System.Net;
using System.Text.Json;

namespace MeticulousResearch.Core.Credentials;

/// <summary>
/// <see cref="IKeyTester"/> that GETs <c>/v1/models</c> at the resolved base URL with the resolved
/// key (settings-secure-key/phase.md). The endpoint is never hardcoded — it is derived from
/// <see cref="IApiCredentialProvider.ResolveBaseUrl"/>. Errors are translated to actionable
/// messages with no stack traces.
/// </summary>
public sealed class KeyTester : IKeyTester
{
    private const string ModelsPath = "/v1/models";

    private readonly IApiCredentialProvider _credentials;
    private readonly HttpClient _httpClient;

    /// <summary>Creates the tester over the credential provider and an (injected) HTTP client.</summary>
    public KeyTester(IApiCredentialProvider credentials, HttpClient httpClient)
    {
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public async Task<KeyTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        var key = _credentials.ResolveApiKey();
        if (string.IsNullOrEmpty(key))
            return KeyTestResult.Failure("No API key is configured. Enter a key before testing.");

        var baseUrl = _credentials.ResolveBaseUrl().TrimEnd('/');
        var requestUri = baseUrl + ModelsPath;

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("x-api-key", key);
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return KeyTestResult.Failure("The API key is invalid. Check the key and try again.");

            if (!response.IsSuccessStatusCode)
                return KeyTestResult.Failure(
                    $"The API returned an unexpected response ({(int)response.StatusCode}). Please try again.");

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return KeyTestResult.Ok(ParseModels(body));
        }
        catch (HttpRequestException)
        {
            return KeyTestResult.Failure(
                "Could not reach the API. Check your network connection and base URL, then try again.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return KeyTestResult.Failure("The request timed out. Check your network connection and try again.");
        }
    }

    private static IReadOnlyList<string> ParseModels(string body)
    {
        var models = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        var value = id.GetString();
                        if (!string.IsNullOrEmpty(value))
                            models.Add(value!);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // A malformed body still counts as a reachable, authorized endpoint; return no models.
        }

        return models;
    }
}
