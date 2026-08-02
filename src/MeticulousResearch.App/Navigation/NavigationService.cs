using CommunityToolkit.Mvvm.ComponentModel;
using MeticulousResearch.App.ViewModels;

namespace MeticulousResearch.App.Navigation;

/// <summary>
/// Default <see cref="INavigationService"/>. Keeps a back-stack of <see cref="NavEntry"/> and
/// resolves destination view-models through an injected factory (the DI container in the app,
/// a simple lambda in tests). Deliberately window-free so it is <c>@unit</c>-testable without a
/// running WPF window (TESTING-STRATEGY §2).
/// </summary>
public sealed class NavigationService : ObservableObject, INavigationService
{
    private readonly Func<Type, object[], ViewModelBase> _resolver;
    private readonly Stack<NavEntry> _backStack = new();

    /// <summary>
    /// Creates a navigation service that resolves view-models via <paramref name="resolver"/>.
    /// </summary>
    /// <param name="resolver">
    /// Maps a view-model <see cref="Type"/> (plus the navigation parameters, so project-scoped
    /// view-models can be constructed with their project id) to an instance. In the app this
    /// delegates to the DI container; in tests it can be a plain factory over fakes.
    /// </param>
    public NavigationService(Func<Type, object[], ViewModelBase> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    private ViewModelBase? _currentViewModel;

    /// <inheritdoc />
    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    private string? _activeProjectId;

    /// <inheritdoc />
    public string? ActiveProjectId
    {
        get => _activeProjectId;
        private set => SetProperty(ref _activeProjectId, value);
    }

    /// <inheritdoc />
    public bool CanGoBack => _backStack.Count > 1;

    /// <inheritdoc />
    public TViewModel NavigateTo<TViewModel>(params object[] parameters) where TViewModel : ViewModelBase
    {
        parameters ??= Array.Empty<object>();
        var vm = (TViewModel)_resolver(typeof(TViewModel), parameters);
        Activate(vm, parameters);
        _backStack.Push(new NavEntry(vm, parameters));
        OnPropertyChanged(nameof(CanGoBack));
        return vm;
    }

    /// <inheritdoc />
    public void Back()
    {
        if (!CanGoBack)
        {
            return;
        }

        _backStack.Pop();
        var previous = _backStack.Peek();
        Activate(previous.ViewModel, previous.Parameters);
        OnPropertyChanged(nameof(CanGoBack));
    }

    private void Activate(ViewModelBase vm, object[] parameters)
    {
        if (vm is INavigationAware aware)
        {
            aware.OnNavigatedTo(parameters);
        }

        // Track the active project whenever we enter a project-scoped destination; leaving to a
        // project-less destination (e.g. Projects home) clears it.
        ActiveProjectId = vm is IProjectScoped scoped ? scoped.ProjectId : null;

        CurrentViewModel = vm;
    }
}
