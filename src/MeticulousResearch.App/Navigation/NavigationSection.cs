namespace MeticulousResearch.App.Navigation;

/// <summary>
/// The sections available in a project's left navigation. The order here is the order the
/// left-nav renders them (SPEC §4.2). <see cref="Dashboard"/> is the project's default view.
/// </summary>
public enum NavigationSection
{
    /// <summary>Conversations (model-selectable Q&amp;A threads).</summary>
    Conversations,

    /// <summary>Resources (project source material).</summary>
    Resources,

    /// <summary>Artifacts (generated deliverables).</summary>
    Artifacts,

    /// <summary>Project dashboard — the default view when a project opens.</summary>
    Dashboard,

    /// <summary>Project-scoped settings.</summary>
    Settings,
}
