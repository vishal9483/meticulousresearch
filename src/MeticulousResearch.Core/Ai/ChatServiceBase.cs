using System.Runtime.CompilerServices;
using MeticulousResearch.Core.Credentials;

namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Shared behavior for both backends: resolve the effective key/base URL through
/// <see cref="IApiCredentialProvider"/> at request time, surface a clear "no API key" fault before
/// any request when none is configured, and otherwise assemble the request and delegate streaming to
/// the concrete backend. Centralizing this guarantees env-wins credential handling and identical
/// request assembly across the sidecar and direct-API paths.
/// </summary>
public abstract class ChatServiceBase : IChatService
{
    /// <summary>Resolves the effective key and base URL (env wins, else secure store / setting).</summary>
    protected IApiCredentialProvider Credentials { get; }

    /// <summary>Assembles the backend-agnostic request payload.</summary>
    protected ChatRequestAssembler Assembler { get; }

    /// <summary>Creates the base over the credential provider and request assembler.</summary>
    protected ChatServiceBase(IApiCredentialProvider credentials, ChatRequestAssembler assembler)
    {
        Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        Assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatEvent> Ask(
        ChatAskContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var apiKey = Credentials.ResolveApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            yield return new ChatFaulted(ChatErrorKind.MissingApiKey, false, ChatErrorMessages.MissingApiKey);
            yield break;
        }

        var baseUrl = Credentials.ResolveBaseUrl();
        var request = Assembler.Assemble(context, apiKey!, baseUrl);

        await foreach (var chatEvent in Stream(request, cancellationToken).WithCancellation(cancellationToken))
            yield return chatEvent;
    }

    /// <summary>
    /// Streams the assembled request against the concrete backend (sidecar or direct API). A
    /// well-formed stream is zero or more <see cref="ChatTokenDelta"/> then a single terminal event.
    /// </summary>
    protected abstract IAsyncEnumerable<ChatEvent> Stream(ChatRequest request, CancellationToken cancellationToken);
}
