using MeticulousResearch.Core.Credentials;

namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The pure-C# direct Anthropic Messages API backend (SPEC §7.2) — the no-sidecar fallback. It
/// implements the same <see cref="IChatService"/> contract as the sidecar: it resolves credentials,
/// assembles the request, and streams tokens/usage via an injected <see cref="IDirectApiTransport"/>.
/// No Node runtime and no sidecar process are involved.
/// </summary>
public sealed class DirectApiChatService : ChatServiceBase
{
    private readonly IDirectApiTransport _transport;

    /// <summary>Creates the direct-API backend over the credential provider, assembler, and transport.</summary>
    public DirectApiChatService(
        IApiCredentialProvider credentials,
        ChatRequestAssembler assembler,
        IDirectApiTransport transport)
        : base(credentials, assembler)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <inheritdoc />
    protected override IAsyncEnumerable<ChatEvent> Stream(ChatRequest request, CancellationToken cancellationToken) =>
        _transport.SendAsync(request, cancellationToken);
}
