using MeticulousResearch.App.Navigation;

namespace MeticulousResearch.App.ViewModels.Sections;

/// <summary>
/// Conversations section — model-selectable Q&amp;A threads scoped to the project (SPEC §3.3).
/// Minimal but designed: real content lands with the <c>conversations</c> feature.
/// </summary>
public sealed class ConversationsViewModel : SectionViewModel
{
    /// <summary>Creates the Conversations section for <paramref name="projectId"/>.</summary>
    public ConversationsViewModel(string projectId) : base(projectId) { }

    /// <inheritdoc />
    public override NavigationSection Section => NavigationSection.Conversations;

    /// <inheritdoc />
    public override string Title => "Conversations";

    /// <summary>Designed one-line description of what this section is for.</summary>
    public string Headline => "Grounded, model-selectable Q&A threads for this project.";
}
