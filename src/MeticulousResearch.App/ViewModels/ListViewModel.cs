using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.Core.ViewStates;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// Base view-model for a list surface (SPEC §3.7). Owns an observable <see cref="Items"/> collection
/// and derives the shared <see cref="ViewState"/> from it: an empty collection shows the designed
/// empty state with a working call-to-action; the first item added leaves the empty state for
/// content. Loading and error transitions are inherited from <see cref="StatefulViewModel"/>. Every
/// primary list (Projects home, Resources, Conversations, Artifacts) derives from this so no pane is
/// ever blank.
/// </summary>
/// <typeparam name="T">The list item type.</typeparam>
public abstract class ListViewModel<T> : StatefulViewModel
{
    /// <summary>Creates the list view-model, starting in the empty state until data loads.</summary>
    /// <param name="errorMapper">Maps failures to human-readable messages + recovery actions.</param>
    protected ListViewModel(IUserErrorMapper errorMapper)
        : base(errorMapper)
    {
        Items.CollectionChanged += OnItemsChanged;
        RecomputeContentState();
    }

    /// <summary>The list's items. Views bind to this only via <see cref="StatefulViewModel.State"/>.</summary>
    public ObservableCollection<T> Items { get; } = new();

    /// <summary>The label of the empty-state call-to-action (e.g. "New project"). Never blank.</summary>
    public abstract string CallToActionLabel { get; }

    /// <summary>The command invoked by the empty-state call-to-action. Never <c>null</c>.</summary>
    public abstract IRelayCommand CallToActionCommand { get; }

    /// <summary>
    /// Loads the list under the state machine: shows <see cref="ViewState.Loading"/> while
    /// <paramref name="loader"/> runs, then replaces <see cref="Items"/> and shows
    /// <see cref="ViewState.Empty"/> or <see cref="ViewState.Content"/> depending on the result, or
    /// an error state if the loader throws.
    /// </summary>
    /// <param name="loader">Produces the list's items asynchronously.</param>
    protected Task LoadAsync(Func<Task<IReadOnlyList<T>>> loader)
    {
        if (loader is null)
            throw new ArgumentNullException(nameof(loader));

        return RunGuardedAsync(async () =>
        {
            var items = await loader().ConfigureAwait(true);
            Replace(items);
        });
    }

    /// <summary>Replaces the list's items and recomputes the empty/content state.</summary>
    protected void Replace(IEnumerable<T> items)
    {
        Items.CollectionChanged -= OnItemsChanged;
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
        Items.CollectionChanged += OnItemsChanged;
        RecomputeContentState();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RecomputeContentState();

    /// <summary>
    /// Maps the item count to <see cref="ViewState.Empty"/> or <see cref="ViewState.Content"/>. Does
    /// not clobber an active Error state — that is owned by the load lifecycle and cleared on retry.
    /// </summary>
    private void RecomputeContentState()
    {
        if (State is ViewState.Error)
            return;
        State = Items.Count == 0 ? ViewState.Empty : ViewState.Content;
    }
}
