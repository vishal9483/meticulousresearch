using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.ViewStates;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// Base view-model that owns the shared <see cref="ViewState"/> machine (SPEC §3.7). Every async
/// view derives from this so its Loading → Content / Empty / Error transitions are
/// <c>@unit</c>-testable without a window, and so a failed operation always surfaces a
/// human-readable <see cref="UserError"/> with a working recovery action instead of a raw stack
/// trace. Owned by the <c>empty-loading-error-states</c> feature.
/// </summary>
public abstract partial class StatefulViewModel : ViewModelBase
{
    private readonly IUserErrorMapper _errorMapper;
    private Func<Task>? _lastOperation;

    /// <summary>Creates the base view-model over the shared error mapper.</summary>
    /// <param name="errorMapper">Maps failures to human-readable messages + recovery actions.</param>
    protected StatefulViewModel(IUserErrorMapper errorMapper)
    {
        _errorMapper = errorMapper ?? throw new ArgumentNullException(nameof(errorMapper));
        RecoveryCommand = new AsyncRelayCommand(RecoverAsync, () => Error is not null);
    }

    [ObservableProperty]
    private ViewState _state;

    [ObservableProperty]
    private UserError? _error;

    /// <summary>
    /// Re-runs the operation that last failed (the error state's recovery action, e.g. "Retry").
    /// Enabled only while an <see cref="Error"/> is showing.
    /// </summary>
    public IAsyncRelayCommand RecoveryCommand { get; }

    /// <summary>True while a load is in flight — drives the skeleton loader.</summary>
    public bool IsLoading => State == ViewState.Loading;

    /// <summary>True while a designed empty state is showing.</summary>
    public bool IsEmptyState => State == ViewState.Empty;

    /// <summary>True while content is showing.</summary>
    public bool HasContent => State == ViewState.Content;

    /// <summary>True while an error state is showing.</summary>
    public bool HasError => State == ViewState.Error;

    /// <summary>The label of the current recovery action, or <c>null</c> when not in error.</summary>
    public string? RecoveryActionLabel => Error?.RecoveryAction;

    /// <summary>
    /// Runs <paramref name="operation"/> guarded by the state machine: sets
    /// <see cref="ViewState.Loading"/>, then leaves the operation to set the resulting state on
    /// success, or maps any exception to an <see cref="Error"/> (remembering the operation so the
    /// recovery action can re-run it) on failure.
    /// </summary>
    /// <param name="operation">The async operation to run under the state machine.</param>
    protected async Task RunGuardedAsync(Func<Task> operation)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        _lastOperation = operation;
        Error = null;
        State = ViewState.Loading;
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetError(_errorMapper.FromException(ex, GetType().Name));
        }
    }

    /// <summary>Places the view-model into the error state with the given user-facing error.</summary>
    protected void SetError(UserError error)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
        State = ViewState.Error;
    }

    private async Task RecoverAsync()
    {
        if (_lastOperation is { } op)
            await RunGuardedAsync(op).ConfigureAwait(true);
    }

    partial void OnStateChanged(ViewState value)
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnErrorChanged(UserError? value)
    {
        OnPropertyChanged(nameof(RecoveryActionLabel));
        RecoveryCommand.NotifyCanExecuteChanged();
    }
}
