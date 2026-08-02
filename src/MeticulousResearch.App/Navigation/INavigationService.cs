using System.ComponentModel;
using MeticulousResearch.App.ViewModels;

namespace MeticulousResearch.App.Navigation;

/// <summary>
/// The application-wide navigation contract owned by the app-shell-navigation feature.
/// Later features add destinations by registering a view-model + DataTemplate and calling
/// <see cref="NavigateTo{TViewModel}"/>; they must not replace this contract.
/// </summary>
/// <remarks>
/// Implements <see cref="INotifyPropertyChanged"/> so the shell can bind directly to
/// <see cref="CurrentViewModel"/> / <see cref="ActiveProjectId"/> / <see cref="CanGoBack"/>.
/// </remarks>
public interface INavigationService : INotifyPropertyChanged
{
    /// <summary>The view-model currently shown in the shell's content region.</summary>
    ViewModelBase? CurrentViewModel { get; }

    /// <summary>
    /// The id of the project the user is currently working inside, or <c>null</c> when at a
    /// project-less destination (e.g. the Projects home). Set whenever navigation enters a
    /// project-scoped view-model that carries a project id.
    /// </summary>
    string? ActiveProjectId { get; }

    /// <summary>True when there is a previous entry on the back-stack.</summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Resolves <typeparamref name="TViewModel"/> from the container, initializes it with the
    /// supplied parameters, records the destination on the back-stack, and makes it the
    /// <see cref="CurrentViewModel"/>. If the resolved view-model is project-scoped, updates
    /// <see cref="ActiveProjectId"/>.
    /// </summary>
    /// <typeparam name="TViewModel">The destination view-model type.</typeparam>
    /// <param name="parameters">
    /// Optional navigation parameters. If the view-model implements
    /// <see cref="INavigationAware"/> it receives them via <see cref="INavigationAware.OnNavigatedTo"/>.
    /// </param>
    /// <returns>The activated view-model instance.</returns>
    TViewModel NavigateTo<TViewModel>(params object[] parameters) where TViewModel : ViewModelBase;

    /// <summary>
    /// Pops the current entry and re-activates the previous one, restoring its parameters.
    /// No-op when <see cref="CanGoBack"/> is false.
    /// </summary>
    void Back();
}
