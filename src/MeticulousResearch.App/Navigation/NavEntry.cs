using MeticulousResearch.App.ViewModels;

namespace MeticulousResearch.App.Navigation;

/// <summary>
/// A single entry on the navigation back-stack: the activated view-model instance plus the
/// parameters it was navigated with (so re-activation via <see cref="INavigationService.Back"/>
/// can restore state).
/// </summary>
/// <param name="ViewModel">The view-model instance for this entry.</param>
/// <param name="Parameters">The parameters passed on the navigation that created this entry.</param>
public sealed record NavEntry(ViewModelBase ViewModel, object[] Parameters);
