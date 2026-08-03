using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.Core.Conversations;
using MeticulousResearch.Core.Settings;

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
    private readonly string _model;
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
    /// model). When <paramref name="conversations"/> is null the section is design-time only.
    /// </summary>
    public ConversationsViewModel(string projectId, IConversationService? conversations, ISettingsService? settings)
        : base(projectId)
    {
        _conversations = conversations;
        _model = settings?.DefaultModel ?? SettingsService.DefaultModelValue;
        Turns = new ReadOnlyObservableCollection<ConversationTurnViewModel>(_turns);
    }

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

    /// <summary>Whether a turn is currently in flight (a stop/streaming hook for later features).</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Sends the composed message: appends the user turn, drives generation through the
    /// conversation service, and appends the assistant reply below it. Ignores blank input.
    /// </summary>
    [RelayCommand]
    private async Task Send()
    {
        var text = (Draft ?? "").Trim();
        if (text.Length == 0 || IsBusy)
            return;

        Draft = "";
        AppendTurn("user", text);

        if (_conversations is null)
            return; // Design-time: no backend wired.

        IsBusy = true;
        try
        {
            _conversationId ??= _conversations.Create(ProjectId).Id;
            var assistant = await _conversations.Ask(_conversationId, text, _model).ConfigureAwait(true);
            AppendTurn("assistant", assistant.Content);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AppendTurn(string role, string content)
    {
        _turns.Add(new ConversationTurnViewModel(role, content));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
