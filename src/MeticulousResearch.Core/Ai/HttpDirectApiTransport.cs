using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Production <see cref="IDirectApiTransport"/>: streams the Anthropic Messages API over HTTP SSE to
/// the request's resolved <see cref="ChatRequest.BaseUrl"/> using its resolved
/// <see cref="ChatRequest.ApiKey"/> (SPEC §7.2, §7.5). The endpoint is never hardcoded — it is taken
/// from the request. Transient failures (429 / 5xx) are surfaced as retryable
/// <see cref="ChatFaulted"/> events via <see cref="ChatErrorClassifier"/>.
/// </summary>
public sealed class HttpDirectApiTransport : IDirectApiTransport
{
    private const string AnthropicVersion = "2023-06-01";
    private const int DefaultMaxTokens = 4096;

    private readonly HttpClient _http;

    /// <summary>Creates the transport over a shared <see cref="HttpClient"/>.</summary>
    public HttpDirectApiTransport(HttpClient http) =>
        _http = http ?? throw new ArgumentNullException(nameof(http));

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatEvent> SendAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{request.BaseUrl}/v1/messages")
        {
            Content = new StringContent(BuildBody(request), Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", request.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);

        using var response = await _http
            .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var (kind, retryable) = ChatErrorClassifier.FromStatusCode((int)response.StatusCode);
            yield return new ChatFaulted(kind, retryable,
                $"The generation request failed ({(int)response.StatusCode}). Please try again.");
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var text = new StringBuilder();
        long input = 0, output = 0, cacheRead = 0, cacheWrite = 0;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var payload = line["data:".Length..].Trim();
            if (payload.Length == 0 || payload == "[DONE]")
                continue;

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

            switch (type)
            {
                case "message_start":
                    if (root.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("usage", out var startUsage))
                    {
                        input = ReadLong(startUsage, "input_tokens");
                        cacheRead = ReadLong(startUsage, "cache_read_input_tokens");
                        cacheWrite = ReadLong(startUsage, "cache_creation_input_tokens");
                    }
                    break;

                case "content_block_delta":
                    if (root.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("text", out var deltaText))
                    {
                        var chunk = deltaText.GetString() ?? string.Empty;
                        if (chunk.Length > 0)
                        {
                            text.Append(chunk);
                            yield return new ChatTokenDelta(chunk);
                        }
                    }
                    break;

                case "message_delta":
                    if (root.TryGetProperty("usage", out var deltaUsage))
                        output = ReadLong(deltaUsage, "output_tokens");
                    break;

                case "message_stop":
                    yield return new ChatCompleted(
                        text.ToString(),
                        new ChatUsage(input, output, cacheRead, cacheWrite));
                    yield break;
            }
        }

        // Stream ended without an explicit message_stop; still deliver a completion.
        yield return new ChatCompleted(text.ToString(), new ChatUsage(input, output, cacheRead, cacheWrite));
    }

    private static long ReadLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : 0;

    private static string BuildBody(ChatRequest request)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("model", request.Model);
            writer.WriteNumber("max_tokens", DefaultMaxTokens);
            writer.WriteBoolean("stream", true);

            var system = ComposeSystem(request);
            if (system.Length > 0)
                writer.WriteString("system", system);

            writer.WriteStartArray("messages");
            foreach (var turn in request.History)
                WriteMessage(writer, turn.Role, turn.Content);
            WriteMessage(writer, "user", request.UserMessage);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string ComposeSystem(ChatRequest request)
    {
        if (request.Resources.Count == 0)
            return request.System;

        var builder = new StringBuilder(request.System);
        foreach (var resource in request.Resources)
        {
            if (builder.Length > 0)
                builder.Append("\n\n");
            builder.Append("# ").Append(resource.Title).Append('\n').Append(resource.Text);
        }
        return builder.ToString();
    }

    private static void WriteMessage(Utf8JsonWriter writer, string role, string content)
    {
        writer.WriteStartObject();
        writer.WriteString("role", role);
        writer.WriteString("content", content);
        writer.WriteEndObject();
    }
}
