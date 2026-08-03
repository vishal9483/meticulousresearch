namespace MeticulousResearch.Core.Ai.Backoff;

/// <summary>Production <see cref="IRetryDelay"/> backed by <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
public sealed class SystemRetryDelay : IRetryDelay
{
    /// <inheritdoc />
    public Task Wait(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration < TimeSpan.Zero ? TimeSpan.Zero : duration, cancellationToken);
}
