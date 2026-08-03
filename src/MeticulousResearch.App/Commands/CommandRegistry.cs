using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.Projects;

namespace MeticulousResearch.App.Commands;

/// <summary>
/// The default <see cref="ICommandRegistry"/> (SPEC §3.5). Builds the four static core commands
/// backed by <see cref="ICommandActions"/> and a dynamic "Go to project: {name}" command per
/// project (from <see cref="IProjectService.List"/>) that navigates into that project's workspace
/// via the shared <see cref="INavigationService"/>.
/// </summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly IProjectService _projects;
    private readonly INavigationService _navigation;
    private readonly ICommandActions _actions;

    /// <summary>Creates the registry over the project list, navigation, and the core action delegates.</summary>
    /// <param name="projects">Supplies the projects the jump-to entries are built from.</param>
    /// <param name="navigation">Navigates into a project workspace when a jump-to entry is chosen.</param>
    /// <param name="actions">The create/search flows the core commands invoke.</param>
    public CommandRegistry(IProjectService projects, INavigationService navigation, ICommandActions actions)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    /// <inheritdoc />
    public IReadOnlyList<PaletteCommand> GetCommands()
    {
        var commands = new List<PaletteCommand>
        {
            new("new-project", "New project",
                new[] { "create", "project", "new" }, _actions.NewProject, "Ctrl+N"),
            new("new-conversation", "New conversation",
                new[] { "create", "conversation", "chat", "ask", "new" }, _actions.NewConversation, "Ctrl+Shift+N"),
            new("new-artifact", "New artifact",
                new[] { "create", "artifact", "document", "deliverable", "new" }, _actions.NewArtifact, "Ctrl+Shift+A"),
            new("search", "Search",
                new[] { "find", "search" }, _actions.OpenSearch, "Ctrl+K"),
        };

        foreach (var project in _projects.List())
        {
            var id = project.Id;
            commands.Add(new PaletteCommand(
                $"go-to-project:{id}",
                $"Go to project: {project.Name}",
                new[] { "go to", "open", "project", "jump", project.Name },
                () => _navigation.NavigateTo<ProjectWorkspaceViewModel>(id)));
        }

        return commands;
    }
}
