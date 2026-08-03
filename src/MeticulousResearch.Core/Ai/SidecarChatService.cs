using System.Runtime.CompilerServices;
using MeticulousResearch.Core.Credentials;

namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The Agent SDK sidecar backend (SPEC §7.2) — the primary path. It resolves credentials, assembles
/// the request, ensures a supervised sidecar is running (auto-restarting a crashed one), and streams
/// its <c>query()</c> results as <see cref="ChatEvent"/>. The API key travels to the sidecar over the
/// authenticated channel per request, never on the command line (SPEC §7.5). A crash mid-stream
/// surfaces a <b>retryable</b> fault so the turn is never silently lost; repeated launch crashes
/// surface a non-retryable "backend unavailable" fault.
/// </summary>
public sealed class SidecarChatService : ChatServiceBase
{
    private readonly SidecarSupervisor _supervisor;

    /// <summary>Creates the sidecar backend over credentials, the assembler, and the supervisor.</summary>
    public SidecarChatService(
        IApiCredentialProvider credentials,
        ChatRequestAssembler assembler,
        SidecarSupervisor supervisor)
        : base(credentials, assembler)
    {
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<ChatEvent> Stream(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ISidecarProcess? process = null;
        SidecarUnavailableException? unavailable = null;
        try
        {
            process = _supervisor.EnsureRunning(new SidecarStartInfo(request.BaseUrl));
        }
        catch (SidecarUnavailableException ex)
        {
            unavailable = ex;
        }

        if (unavailable is not null)
        {
            yield return new ChatFaulted(ChatErrorKind.BackendUnavailable, false, unavailable.Message);
            yield break;
        }

        var enumerator = process!.Send(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                ChatEvent current;
                var crashed = false;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        yield break;
                    current = enumerator.Current;
                }
                catch (SidecarCrashedException)
                {
                    crashed = true;
                    current = null!;
                }

                if (crashed)
                {
                    // Preserve the partial tokens already delivered; signal a retryable fault.
                    yield return new ChatFaulted(
                        ChatErrorKind.Transport, true, ChatErrorMessages.InterruptedRetryable);
                    yield break;
                }

                yield return current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }
}
