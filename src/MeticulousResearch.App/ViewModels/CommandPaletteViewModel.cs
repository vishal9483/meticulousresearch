using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.Commands;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// The command-palette view-model (SPEC §3.5): turns a query into a ranked list of
/// <see cref="PaletteCommand"/> results (fuzzy/substring match over each command's display name and
/// keywords), supports keyboard selection, exposes a designed "no matching commands" empty state,
/// and invokes the chosen command on activation. All matching/ranking lives here so it is
/// <c>@unit</c>-testable without a window; the view is thin wiring proven by <c>@ui</c>.
/// </summary>
public sealed partial class CommandPaletteViewModel : ViewModelBase
{
    /// <summary>The empty-state message shown when a query matches no commands (SPEC §9.1(10)).</summary>
    public const string NoResultsMessage = "No matching commands";

    private readonly ICommandRegistry _registry;

    /// <summary>Creates the palette over the command registry.</summary>
    /// <param name="registry">The catalog of invokable commands to search and rank.</param>
    public CommandPaletteViewModel(ICommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Refresh();
    }

    /// <summary>The ranked results for the current <see cref="Query"/>.</summary>
    public ObservableCollection<PaletteCommand> Results { get; } = new();

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private int _selectedIndex = -1;

    partial void OnQueryChanged(string value) => Refresh();

    /// <summary>True when the current query matches no commands — drives the designed empty state.</summary>
    public bool IsEmptyState => Results.Count == 0;

    /// <summary>Whether at least one result is showing.</summary>
    public bool HasResults => Results.Count > 0;

    /// <summary>The empty-state message (no raw error is ever shown, SPEC §9.1(10)).</summary>
    public string EmptyStateMessage => NoResultsMessage;

    /// <summary>The currently highlighted command, or <c>null</c> when there is no valid selection.</summary>
    public PaletteCommand? SelectedCommand =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? Results[SelectedIndex] : null;

    /// <summary>Resets the palette to its opened state: clears the query and highlights the first result.</summary>
    public void Open()
    {
        Query = "";
        Refresh();
    }

    /// <summary>Moves the highlight to the next result (wraps within bounds).</summary>
    [RelayCommand]
    public void MoveSelectionDown()
    {
        if (Results.Count == 0)
            return;
        SelectedIndex = Math.Min(SelectedIndex + 1, Results.Count - 1);
    }

    /// <summary>Moves the highlight to the previous result.</summary>
    [RelayCommand]
    public void MoveSelectionUp()
    {
        if (Results.Count == 0)
            return;
        SelectedIndex = Math.Max(SelectedIndex - 1, 0);
    }

    /// <summary>Invokes the currently highlighted command's action. No-op when nothing is selected.</summary>
    [RelayCommand]
    public void Activate() => SelectedCommand?.Execute();

    /// <summary>Invokes the given command's action (used when a result is clicked/chosen directly).</summary>
    /// <param name="command">The command to invoke.</param>
    [RelayCommand]
    public void Choose(PaletteCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
    }

    private void Refresh()
    {
        var query = (Query ?? "").Trim();
        var all = _registry.GetCommands();

        IEnumerable<PaletteCommand> ranked = query.Length == 0
            ? all
            : all
                .Select((command, index) => (command, index, score: Score(command, query)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.index)
                .Select(x => x.command);

        Results.Clear();
        foreach (var command in ranked)
            Results.Add(command);

        SelectedIndex = Results.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(SelectedCommand));
    }

    partial void OnSelectedIndexChanged(int value) => OnPropertyChanged(nameof(SelectedCommand));

    /// <summary>
    /// Scores a command against the query across its display name and keywords: exact &gt; prefix
    /// &gt; substring &gt; subsequence (fuzzy). A score of 0 means no match.
    /// </summary>
    private static int Score(PaletteCommand command, string query)
    {
        var q = query.ToLowerInvariant();
        var best = 0;

        foreach (var candidate in Candidates(command))
        {
            var c = candidate.ToLowerInvariant();
            int score;
            if (c == q)
                score = 1000;
            else if (c.StartsWith(q, StringComparison.Ordinal))
                score = 500;
            else if (c.Contains(q, StringComparison.Ordinal))
                score = 300;
            else if (IsSubsequence(q, c))
                score = 100;
            else
                score = 0;

            if (score > best)
                best = score;
        }

        return best;
    }

    private static IEnumerable<string> Candidates(PaletteCommand command)
    {
        yield return command.DisplayName;
        foreach (var keyword in command.Keywords)
            yield return keyword;
    }

    private static bool IsSubsequence(string query, string candidate)
    {
        var i = 0;
        foreach (var ch in candidate)
        {
            if (i < query.Length && query[i] == ch)
                i++;
        }
        return i == query.Length;
    }
}
