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
                $"The generation request failed ({(int)response.StatusCode}). Please try again.")
            {
                // Honor the server's retry-after hint so rate-limit-backoff waits at least that long (SPEC §8).
                RetryAfter = ReadRetryAfter(response),
            };
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

    /// <summary>Reads the <c>Retry-After</c> header (delta-seconds or HTTP-date) into a <see cref="TimeSpan"/>, if present.</summary>
    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
            return null;
        if (retryAfter.Delta is { } delta)
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        if (retryAfter.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }
        return null;
    }

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
                WriteSystem(writer, system, request.CacheBreakpoints.Count > 0);

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

    /// <summary>
    /// Writes the composed system prompt (custom instructions + stable resource context). When the
    /// request carries a cache breakpoint (prompt-caching, SPEC §8), the stable system block is emitted
    /// as a structured content block terminated by an ephemeral <c>cache_control</c> marker so repeated
    /// turns and regenerations reuse the cached input; otherwise it is sent as a plain string.
    /// </summary>
    private static void WriteSystem(Utf8JsonWriter writer, string system, bool cacheable)
    {
        if (!cacheable)
        {
            writer.WriteString("system", system);
            return;
        }

        writer.WriteStartArray("system");
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", system);
        writer.WriteStartObject("cache_control");
        writer.WriteString("type", "ephemeral");
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndArray();
    }

    private static void WriteMessage(Utf8JsonWriter writer, string role, string content)
    {
        writer.WriteStartObject();
        writer.WriteString("role", role);
        writer.WriteString("content", content);
        writer.WriteEndObject();
    }
}