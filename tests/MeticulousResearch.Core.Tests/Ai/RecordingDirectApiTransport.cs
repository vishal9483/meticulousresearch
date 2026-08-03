using System.Runtime.CompilerServices;
using System.Text;
using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.Core.Tests.Ai;

/// <summary>
/// Recording <see cref="IDirectApiTransport"/>: captures the assembled <see cref="ChatRequest"/> the
/// direct-API backend would send and replays a scripted event sequence — no network.
/// </summary>
internal sealed class RecordingDirectApiTransport : IDirectApiTransport
{
    private readonly List<ChatEvent> _script = new();

    public ChatRequest? LastRequest { get; private set; }
    public int SendCount { get; private set; }

    public RecordingDirectApiTransport Script(params ChatEvent[] events)
    {
        _script.AddRange(events);
        return this;
    }

    public RecordingDirectApiTransport ScriptTokensThenComplete(ChatUsage usage, params string[] tokens)
    {
        var text = new StringBuilder();
        foreach (var t in tokens)
        {
            _script.Add(new ChatTokenDelta(t));
            text.Append(t);
        }
        _script.Add(new ChatCompleted(text.ToString(), usage));
        return this;
    }

    public async IAsyncEnumerable<ChatEvent> SendAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        SendCount++;
        LastRequest = request;
        foreach (var e in _script)
        {
            await Task.Yield();
            yield return e;
        }
    }
}
