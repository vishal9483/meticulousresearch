namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The transport used by <see cref="DirectApiChatService"/> to stream an assembled request against
/// the Anthropic Messages API. Abstracted so the direct-API backend is <c>@unit</c>-testable without
/// a network round trip; the production implementation is <see cref="HttpDirectApiTransport"/>.
/// </summary>
public interface IDirectApiTransport
{
    /// <summary>
    /// Streams <paramref name="request"/> to <see cref="ChatRequest.BaseUrl"/> using
    /// <see cref="ChatRequest.ApiKey"/>, yielding token deltas then a terminal event.
    /// </summary>
    IAsyncEnumerable<ChatEvent> SendAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
