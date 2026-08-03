using CommunityToolkit.Mvvm.ComponentModel;
using MeticulousResearch.Core.Ai.Backoff;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// The window-free "retrying…" indicator for a conversation (SPEC §8 / rate-limit-backoff): it
/// observes the backoff state transitions of <see cref="RetryingChatService"/> via
/// <see cref="IRetryObserver"/> and exposes a non-alarming, attempt-numbered status the view binds
/// to. It is deliberately not an error surface — no dialog, no raw status code. The indicator shows
/// while a turn is being retried and clears the moment the turn resolves (success or final failure).
/// </summary>
public sealed partial class RetryStatusViewModel : ViewModelBase, IRetryObserver
{
    /// <summary>Whether a retry is currently in progress (drives the indicator's visibility).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isRetrying;

    /// <summary>The 1-based number of the attempt currently being retried.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _attempt;

    /// <summary>A non-alarming status line with the attempt count, or empty when not retrying.</summary>
    public string StatusText => IsRetrying ? $"Retrying… (attempt {Attempt})" : "";

    /// <inheritdoc />
    public void OnRetrying(RetryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        IsRetrying = true;
        Attempt = state.Attempt;
    }

    /// <inheritdoc />
    public void OnResolved() => IsRetrying = false;
}
