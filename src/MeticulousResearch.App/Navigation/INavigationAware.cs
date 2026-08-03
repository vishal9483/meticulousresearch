namespace MeticulousResearch.App.Navigation;

/// <summary>
/// Implemented by view-models that need the navigation parameters passed to
/// <see cref="INavigationService.NavigateTo{TViewModel}"/>. Called after construction, both on
/// forward navigation and when a view-model is re-activated by <see cref="INavigationService.Back"/>.
/// </summary>
public interface INavigationAware
{
    /// <summary>Receives the navigation parameters for this activation.</summary>
    void OnNavigatedTo(object[] parameters);
}
