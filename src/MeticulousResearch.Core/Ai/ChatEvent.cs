namespace MeticulousResearch.Core.Ai;

/// <summary>
/// One item in the stream a backend produces for a request. A well-formed stream is zero or more
/// <see cref="ChatTokenDelta"/> items followed by exactly one terminal event —
/// <see cref="ChatCompleted"/>, <see cref="ChatCancelled"/>, or <see cref="ChatFaulted"/>. Every
/// consumer (streaming, conversations, backoff, …) is written against this closed hierarchy and is
/// unaware which backend produced it.
/// </summary>
public abstract record ChatEvent;

/// <summary>An incremental token (or token fragment) of the assistant's answer.</summary>
/// <param name="Text">The delta text to append to the answer so far.</param>
public sealed record ChatTokenDelta(string Text) : ChatEvent;

/// <summary>
/// The successful terminal event carrying the final assembled text and the billed usage.
/// </summary>
/// <param name="Text">The complete assistant answer (the concatenation of every delta).</param>
/// <param name="Usage">The billed token usage for the turn.</param>
public sealed record ChatCompleted(string Text, ChatUsage Usage) : ChatEvent;

/// <summary>
/// The terminal event emitted when the caller cancelled the request. Any tokens delivered before
/// cancellation remain valid; no further tokens follow.
/// </summary>
public sealed record ChatCancelled : ChatEvent;

/// <summary>
/// The terminal event emitted when the turn failed. <paramref name="Retryable"/> tells
/// <c>rate-limit-backoff</c> whether the turn may be retried; <paramref name="Message"/> is a
/// human-readable, actionable message with no raw stack trace.
/// </summary>
/// <param name="Kind">The classified error kind.</param>
/// <param name="Retryable">Whether the caller may retry the turn.</param>
/// <param name="Message">A human-readable, actionable error message (never a stack trace).</param>
public sealed record ChatFaulted(ChatErrorKind Kind, bool Retryable, string Message) : ChatEvent;
