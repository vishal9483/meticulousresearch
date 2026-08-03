using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// A single rendered turn in the conversation thread (SPEC §3.3, §8). Carries the role and text
/// plus simple role flags the view binds to for left/right alignment. For assistant turns it also
/// carries the live <c>streaming</c> state (<see cref="IsStreaming"/> drives the streaming cursor)
/// and the <c>interrupted</c> affordance (<see cref="IsInterrupted"/> plus <see cref="ResumeCommand"/>)
/// so a stopped/faulted turn can be resumed. The cost badge is layered on by <c>turn-metadata-actions</c>.
/// </summary>
public sealed partial class ConversationTurnViewModel : ObservableObject
{
    private readonly Func<ConversationTurnViewModel, Task>? _resume;

    /// <summary>Creates a turn view-model for the given role and content, optionally recording the model that produced it.</summary>
    /// <param name="role">The turn role (<c>user</c> or <c>assistant</c>).</param>
    /// <param name="content">The turn text.</param>
    /// <param name="model">The model id that produced an assistant turn (model-selector), or <c>null</c>.</param>
    /// <param name="resume">Optional callback invoked when the interrupted turn's resume action fires.</param>
    public ConversationTurnViewModel(
        string role,
        string content,
        string? model = null,
        Func<ConversationTurnViewModel, Task>? resume = null)
    {
        Role = role ?? throw new ArgumentNullException(nameof(role));
        _content = content ?? "";
        Model = model;
        _resume = resume;
    }

    /// <summary>The model id that produced this (assistant) turn, or <c>null</c> for user turns / unknown.</summary>
    public string? Model { get; }

    /// <summary>
    /// The id of the persisted <c>Message</c> row backing this turn, once known. Turn actions
    /// (retry/edit/promote/delete, turn-metadata-actions) operate on this id.
    /// </summary>
    public string? MessageId { get; internal set; }

    /// <summary>
    /// The per-turn metadata, cost badge, and actions for a completed assistant turn
    /// (turn-metadata-actions), or <c>null</c> until attached / for user turns. Set once the turn's
    /// persisted metadata is known so the view can bind the badge and action menu.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActions))]
    private TurnActionsViewModel? _actions;

    /// <summary>Whether this turn exposes the metadata/cost/action affordances.</summary>
    public bool HasActions => Actions is not null;

    /// <summary>Whether a model label should be shown for this turn (assistant turns with a recorded model).</summary>
    public bool HasModel => IsAssistant && !string.IsNullOrWhiteSpace(Model);

    /// <summary>The model label shown on the assistant turn (the recorded model id, or empty).</summary>
    public string ModelLabel => Model ?? "";

    /// <summary>The turn role: <c>user</c> or <c>assistant</c>.</summary>
    public string Role { get; }

    private string _content;

    /// <summary>The turn text. Updated live as streaming tokens arrive on an assistant turn.</summary>
    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value ?? "");
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInterrupted))]
    [NotifyPropertyChangedFor(nameof(IsStreamingIndicatorVisible))]
    private bool _isStreaming;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInterrupted))]
    private bool _wasInterrupted;

    /// <summary>Whether the streaming cursor/indicator should be shown (assistant turn, tokens arriving).</summary>
    public bool IsStreamingIndicatorVisible => IsAssistant && IsStreaming;

    /// <summary>Whether this assistant turn was interrupted and offers a resume/retry affordance.</summary>
    public bool IsInterrupted => IsAssistant && WasInterrupted && !IsStreaming;

    /// <summary>Whether this is a user turn.</summary>
    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this is an assistant turn.</summary>
    public bool IsAssistant => !IsUser;

    /// <summary>A short label shown above the turn text.</summary>
    public string RoleLabel => IsUser ? "You" : "Assistant";

    partial void OnIsStreamingChanged(bool value) => ResumeCommand.NotifyCanExecuteChanged();

    partial void OnWasInterruptedChanged(bool value) => ResumeCommand.NotifyCanExecuteChanged();

    /// <summary>Resumes an interrupted assistant turn, continuing its generation.</summary>
    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task Resume()
    {
        if (_resume is not null)
            await _resume(this).ConfigureAwait(true);
    }

    private bool CanResume() => _resume is not null && IsInterrupted;
}
