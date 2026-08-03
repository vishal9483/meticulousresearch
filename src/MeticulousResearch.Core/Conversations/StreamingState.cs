namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// The lifecycle state of an assistant turn that is (or was) streamed token-by-token (SPEC §3.3,
/// §8). A turn starts <see cref="Streaming"/> and reaches exactly one terminal state:
/// <see cref="Completed"/> on a clean finish, or <see cref="Interrupted"/> when the user stops it
/// or the backend faults mid-stream. An <see cref="Interrupted"/> turn is resumable — resuming
/// returns it to <see cref="Streaming"/> and then <see cref="Completed"/>, clearing the interrupted
/// marker.
/// </summary>
public enum StreamingState
{
    /// <summary>Tokens are actively arriving and being appended to the turn.</summary>
    Streaming,

    /// <summary>The stream finished cleanly; the turn holds its final text.</summary>
    Completed,

    /// <summary>The stream was stopped or faulted; the turn holds the accumulated partial text.</summary>
    Interrupted,
}
