using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.Services;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Turns;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Conversations section — project-scoped, grounded Q&amp;A threads (SPEC §3.3, §7.3). Owns the
/// visible thread, the composer, and the designed empty state; it drives generation through
/// <see cref="IConversationService"/> (owned by the <c>conversations</c> feature). Token-by-token
/// streaming, model selection, and per-turn actions are layered on by their own downstream features
/// via the hooks exposed here. Window-free so its logic is <c>@unit</c>-testable.
/// </summary>
public sealed partial class ConversationsViewModel : SectionViewModel
{
    private readonly IConversationService? _conversations;
    private readonly IStreamingConversationService? _streaming;
    private readonly ITurnActionService? _turnActions;
    private readonly ITurnCostCalculator? _costCalculator;
    private readonly IClipboardService? _clipboard;
    private readonly Dictionary<ConversationTurnViewModel, StreamingTurn> _streamingTurns = new();
    private CancellationTokenSource? _activeCts;
    private string? _conversationId;

    /// <summary>
    /// Builds the section without services (window-free plumbing / design-time). Renders the
    /// designed empty state and composer; sending is inert until a service is supplied.
    /// </summary>
    public ConversationsViewModel(string projectId) : this(projectId, null, null)
    {
    }

    /// <summary>
    /// Builds the section wired to the conversation service (and app settings for the default
    /// model). When <paramref name="conversations"/> is null the section is design-time only. The
    /// model picker (model-selector) drives which model each turn is sent with and defaults to the
    /// app/project default model. When a <paramref name="streaming"/> service is supplied, replies
    /// render token-by-token and are stoppable/resumable (SPEC §3.3, §8). When the turn-action
    /// collaborators are supplied, each completed assistant turn exposes its metadata, a per-turn
    /// cost badge, and the turn actions (turn-metadata-actions, SPEC §3.3, §3.6).
    /// </summary>
    public ConversationsViewModel(
        string projectId,
        IConversationService? conversations,
        ISettingsService? settings,
        IModelCatalog? catalog = null,
        IStreamingConversationService? streaming = null,
        ITurnActionService? turnActions = null,
        ITurnCostCalculator? costCalculator = null,
        IClipboardService? clipboard = null,
        RetryStatusViewModel? retryStatus = null)
        : base(projectId)
    {
        _conversations = conversations;
        _streaming = streaming;
        _turnActions = turnActions;
        _costCalculator = costCalculator;
        _clipboard = clipboard;
        RetryStatus = retryStatus ?? new RetryStatusViewModel();
        var initialModel = settings?.DefaultModel ?? SettingsService.DefaultModelValue;
        ModelPicker = new ModelPickerViewModel(catalog ?? ModelCatalogLoader.Default, initialModel);
        Turns = new ReadOnlyObservableCollection<ConversationTurnViewModel>(_turns);
    }


    /// <summary>The tiered model picker (model-selector) that selects the model for turns in this thread.</summary>
    public ModelPickerViewModel ModelPicker { get; }

    /// <summary>
    /// The "retrying…" indicator (rate-limit-backoff, SPEC §8): a non-alarming, attempt-numbered
    /// state shown while a rate-limited/transient turn is being retried; clears on resolution. Wired
    /// to a <see cref="MeticulousResearch.Core.Ai.Backoff.RetryingChatService"/> as its observer.
    /// </summary>
    public RetryStatusViewModel RetryStatus { get; }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Conversations;

    /// <inheritdoc />
    public override string Title => "Conversations";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Grounded, model-selectable Q&A threads for this project.";

    private readonly ObservableCollection<ConversationTurnViewModel> _turns = new();

    /// <summary>The rendered thread of user/assistant turns, oldest first.</summary>
    public ReadOnlyObservableCollection<ConversationTurnViewModel> Turns { get; }

    /// <summary>Whether the thread is empty (drives the designed empty state).</summary>
    public bool IsEmpty => _turns.Count == 0;

    [ObservableProperty]
    private string _draft = "";

    /// <summary>Whether a turn is currently in flight (drives the Stop control and disables Send).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isBusy;

    /// <summary>Whether a stoppable streaming generation is in progress (drives the Stop control's visibility).</summary>
    public bool IsStreaming => IsBusy && _activeCts is not null;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsStreaming));

    /// <summary>
    /// Sends the composed message: appends the user turn, drives generation, and renders the
    /// assistant reply. When a streaming service is wired the reply renders token-by-token into a
    /// live assistant turn that can be stopped mid-stream (SPEC §3.3); otherwise the whole reply is
    /// appended on completion. Ignores blank input.
    /// </summary>
    [RelayCommand]
    private async Task Send()
    {
        var text = (Draft ?? "").Trim();
        if (text.Length == 0 || IsBusy)
            return;

        Draft = "";
        AppendTurn("user", text);

        if (_streaming is not null && _conversations is not null)
        {
            await StreamReply(text).ConfigureAwait(true);
            return;
        }

        if (_conversations is null)
            return; // Design-time: no backend wired.

        IsBusy = true;
        try
        {
            _conversationId ??= _conversations.Create(ProjectId).Id;
            var model = ModelPicker.ResolveForTurn();
            var assistant = await _conversations.Ask(_conversationId, text, model).ConfigureAwait(true);
            var turnVm = new ConversationTurnViewModel("assistant", assistant.Content, assistant.Model)
            {
                MessageId = assistant.Id,
            };
            _turns.Add(turnVm);
            OnPropertyChanged(nameof(IsEmpty));
            AttachActions(turnVm);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StreamReply(string userMessage)
    {
        _conversationId ??= _conversations!.Create(ProjectId).Id;
        var model = ModelPicker.ResolveForTurn();

        var live = new ConversationTurnViewModel("assistant", "", model, ResumeTurn) { IsStreaming = true };
        _turns.Add(live);
        OnPropertyChanged(nameof(IsEmpty));

        using var cts = new CancellationTokenSource();
        _activeCts = cts;
        IsBusy = true;
        try
        {
            var turn = await _streaming!.StreamAsk(
                _conversationId,
                userMessage,
                model,
                onDelta: t => live.Content = t.Text,
                cancellationToken: cts.Token).ConfigureAwait(true);

            _streamingTurns[live] = turn;
            live.Content = turn.Text;
            live.IsStreaming = false;
            live.WasInterrupted = turn.IsInterrupted;
            live.MessageId = turn.PersistedMessageId;
            if (!turn.IsInterrupted)
                AttachActions(live);
        }
        finally
        {
            _activeCts = null;
            IsBusy = false;
        }
    }

    /// <summary>Stops the in-progress generation (SPEC §3.5 Esc/Stop): cancels delivery immediately.</summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _activeCts?.Cancel();

    private bool CanStop() => IsBusy && _activeCts is not null;

    private async Task ResumeTurn(ConversationTurnViewModel live)
    {
        if (_streaming is null || !_streamingTurns.TryGetValue(live, out var turn))
            return;

        using var cts = new CancellationTokenSource();
        _activeCts = cts;
        live.IsStreaming = true;
        live.WasInterrupted = false;
        IsBusy = true;
        try
        {
            var resumed = await _streaming.Resume(
                turn,
                onDelta: t => live.Content = t.Text,
                cancellationToken: cts.Token).ConfigureAwait(true);

            live.Content = resumed.Text;
            live.IsStreaming = false;
            live.WasInterrupted = resumed.IsInterrupted;
        }
        finally
        {
            _activeCts = null;
            IsBusy = false;
        }
    }

    private void AppendTurn(string role, string content, string? model = null)
    {
        _turns.Add(new ConversationTurnViewModel(role, content, model));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// The most recently built promote-to-artifact request (turn-metadata-actions). The artifact
    /// domain is owned by <c>artifact-creation</c> (M3); until it is wired, promoting a turn records
    /// the request here so the provenance-carrying payload is observable.
    /// </summary>
    public PromoteToArtifactRequest? LastPromoteRequest { get; private set; }

    /// <summary>
    /// Attaches the metadata/cost/action affordances to a completed assistant turn when the
    /// turn-action collaborators are wired and the turn has a persisted message id.
    /// </summary>
    private void AttachActions(ConversationTurnViewModel turn)
    {
        if (_turnActions is null || _costCalculator is null || _clipboard is null)
            return;
        if (string.IsNullOrEmpty(turn.MessageId))
            return;

        var messageId = turn.MessageId!;
        var metadata = _turnActions.GetMetadata(messageId);

        turn.Actions = new TurnActionsViewModel(
            turn.Content,
            metadata,
            _costCalculator,
            _clipboard,
            retry: modelOverride => RetryTurn(messageId, modelOverride),
            editResend: newText => EditAndResendTurn(messageId, newText),
            buildPromoteRequest: () => _turnActions.BuildPromoteRequest(messageId),
            promote: request =>
            {
                LastPromoteRequest = request;
                return Task.CompletedTask;
            },
            delete: () => DeleteTurn(turn, messageId));
    }

    private async Task RetryTurn(string messageId, string? modelOverride)
    {
        if (_turnActions is null || _conversationId is null)
            return;
        await _turnActions.Retry(messageId, modelOverride).ConfigureAwait(true);
        RebuildFromPersisted();
    }

    private async Task EditAndResendTurn(string messageId, string newText)
    {
        if (_turnActions is null || _conversationId is null)
            return;
        await _turnActions.EditAndResend(messageId, newText).ConfigureAwait(true);
        RebuildFromPersisted();
    }

    private Task DeleteTurn(ConversationTurnViewModel turn, string messageId)
    {
        if (_turnActions is null)
            return Task.CompletedTask;
        _turnActions.Delete(messageId);
        _turns.Remove(turn);
        OnPropertyChanged(nameof(IsEmpty));
        return Task.CompletedTask;
    }

    /// <summary>Rebuilds the rendered thread from the persisted messages, re-attaching turn actions.</summary>
    private void RebuildFromPersisted()
    {
        if (_conversations is null || _conversationId is null)
            return;

        _turns.Clear();
        _streamingTurns.Clear();
        foreach (var message in _conversations.GetMessages(_conversationId))
        {
            var turn = new ConversationTurnViewModel(message.Role, message.Content, message.Model)
            {
                MessageId = message.Id,
            };
            _turns.Add(turn);
            if (turn.IsAssistant)
                AttachActions(turn);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}