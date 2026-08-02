using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.ViewModels;

namespace MeticulousResearch.App.Tests.Navigation;

/// <summary>
/// Builds a real <see cref="NavigationService"/> with a lightweight reflection-based resolver
/// that mirrors the app's DI resolver: string navigation parameters are forwarded as constructor
/// arguments so project-scoped view-models are built scoped to their project. Keeps @unit tests
/// window- and container-free (TESTING-STRATEGY §2).
/// </summary>
internal static class TestNavigationServiceFactory
{
    public static NavigationService Create() => new((type, parameters) =>
    {
        var ctorArgs = parameters.Where(p => p is string).Cast<object>().ToArray();
        return (ViewModelBase)Activator.CreateInstance(type, ctorArgs)!;
    });
}
