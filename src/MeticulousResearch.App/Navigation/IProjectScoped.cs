namespace MeticulousResearch.App.Navigation;

/// <summary>
/// Implemented by view-models that live inside a specific project. When navigation activates
/// one, the <see cref="INavigationService"/> records its <see cref="ProjectId"/> as the
/// <see cref="INavigationService.ActiveProjectId"/>.
/// </summary>
public interface IProjectScoped
{
    /// <summary>The id of the project this view-model is scoped to.</summary>
    string ProjectId { get; }
}
