using System.Runtime.CompilerServices;
using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.TestSupport;

/// <summary>
/// Scripted <see cref="IChatService"/> for deterministic tests (TESTING-STRATEGY §4), owned by
/// ai-gateway. It replays a fixed sequence of token deltas, a scripted <see cref="ChatUsage"/>, and
/// an optional terminal fault (including 429/5xx classification), so every downstream feature
/// (streaming, conversations, backoff, caching, …) can exercise the contract without a real backend.
/// It records the last <see cref="ChatAskContext"/> and the assembled <see cref="ChatRequest"/> so
/// tests can assert request assembly. Cancellation stops the stream and yields
/// <see cref="ChatCancelled"/>.
/// </summary>
public sealed class FakeChatService : IChatService
{
    private readonly ChatRequestAssembler _assembler = new();
    private readonly List<string> _tokens = new();
    private ChatUsage _usage = ChatUsage.Zero;
    private string? _completionTextOverride;
    private (ChatErrorKind Kind, bool Retryable, string Message)? _fault;

    /// <summary>The key the fake records into the assembled request.</summary>
    public string ApiKey { get; set; } = "sk-fake-key";

    /// <summary>The base URL the fake records into the assembled request.</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>The last context passed to <see cref="Ask"/>, or <c>null</c> if never called.</summary>
    public ChatAskContext? LastContext { get; private set; }

    /// <summary>The last assembled request, or <c>null</c> if <see cref="Ask"/> was never called.</summary>
    public ChatRequest? LastRequest { get; private set; }

    /// <summary>Number of times <see cref="Ask"/> was invoked.</summary>
    public int AskCount { get; private set; }

    /// <summary>Scripts the token deltas the fake will emit, in order.</summary>
    public FakeChatService WithTokens(params string[] tokens)
    {
        _tokens.Clear();
        _tokens.AddRange(tokens);
        return this;
    }

    /// <summary>Scripts the usage reported on completion.</summary>
    public FakeChatService WithUsage(ChatUsage usage)
    {
        _usage = usage;
        return this;
    }

    /// <summary>Scripts usage from raw figures (cache fields default to 0).</summary>
    public FakeChatService WithUsage(long input, long output, long cacheRead = 0, long cacheWrite = 0)
        => WithUsage(new ChatUsage(input, output, cacheRead, cacheWrite));

    /// <summary>Overrides the completion text (default is the concatenation of the token deltas).</summary>
    public FakeChatService WithCompletionText(string text)
    {
        _completionTextOverride = text;
        return this;
    }

    /// <summary>Scripts a terminal fault instead of a completion.</summary>
    public FakeChatService FailWith(ChatErrorKind kind, bool retryable, string message = "scripted failure")
    {
        _fault = (kind, retryable, message);
        return this;
    }

    /// <summary>Scripts a terminal fault classified from an HTTP status code (e.g. 429 or 5xx).</summary>
    public FakeChatService FailWithStatusCode(int statusCode, string message = "scripted failure")
    {
        var (kind, retryable) = ChatErrorClassifier.FromStatusCode(statusCode);
        _fault = (kind, retryable, message);
        return this;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatEvent> Ask(
        ChatAskContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        AskCount++;
        LastContext = context;
        LastRequest = _assembler.Assemble(context, ApiKey, BaseUrl);

        var text = new System.Text.StringBuilder();
        foreach (var token in _tokens)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield return new ChatCancelled();
                yield break;
            }

            text.Append(token);
            yield return new ChatTokenDelta(token);
            await Task.Yield();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            yield return new ChatCancelled();
            yield break;
        }

        if (_fault is { } fault)
        {
            yield return new ChatFaulted(fault.Kind, fault.Retryable, fault.Message);
            yield break;
        }

        yield return new ChatCompleted(_completionTextOverride ?? text.ToString(), _usage);
    }
}
