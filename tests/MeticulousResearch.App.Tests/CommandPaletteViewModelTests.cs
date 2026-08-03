using System.ComponentModel;
using MeticulousResearch.App.Commands;
using MeticulousResearch.App.Navigation;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.App.ViewModels.Sections;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit scenarios from docs/features/command-palette-shortcuts/tests.md — the command registry
/// (core commands + jump-to-project), the palette view-model's ranking/empty-state, and command
/// invocation (create actions + jump-to-project navigation). Window-free.
/// </summary>
public class CommandPaletteViewModelTests
{
    // Scenario: The palette lists the core commands when empty
    //   Given the command palette is open with no query
    //   Then it offers "New project" / "New conversation" / "New artifact" / "Search"
    [Fact]
    public void Palette_lists_the_core_commands_when_empty()
    {
        var vm = NewPalette(out _, out _);

        var names = vm.Results.Select(c => c.DisplayName).ToList();
        Assert.Contains("New project", names);
        Assert.Contains("New conversation", names);
        Assert.Contains("New artifact", names);
        Assert.Contains("Search", names);
    }

    // Scenario Outline: Typing filters commands and jump targets
    //   Given a project named "Semiconductors 2026" exists
    //   And the command palette is open
    //   When I type "<query>"
    //   Then the top result is "<result>"
    [Theory]
    [InlineData("new conv", "New conversation")]
    [InlineData("semic", "Go to project: Semiconductors 2026")]
    [InlineData("search", "Search")]
    public void Typing_filters_commands_and_jump_targets(string query, string expectedTop)
    {
        var projects = new FakeProjectService();
        projects.Create("Semiconductors 2026");
        var vm = NewPalette(out _, out _, projects);

        vm.Query = query;

        Assert.NotEmpty(vm.Results);
        Assert.Equal(expectedTop, vm.Results[0].DisplayName);
    }

    // Scenario: Selecting a jump-to-project result navigates to that project
    //   Given a project named "Energy 2026" exists
    //   And the palette shows "Go to project: Energy 2026"
    //   When I choose it
    //   Then the app navigates to the "Energy 2026" project workspace
    [Fact]
    public void Selecting_a_jump_to_project_result_navigates_to_that_project()
    {
        var projects = new FakeProjectService();
        var energy = projects.Create("Energy 2026");
        var nav = new RecordingParamNavigationService();
        var vm = NewPalette(out _, out _, projects, nav);

        var jump = vm.Results.Single(c => c.DisplayName == "Go to project: Energy 2026");
        vm.ChooseCommand.Execute(jump);

        Assert.Equal(typeof(ProjectWorkspaceViewModel), nav.LastNavigatedTo);
        Assert.Equal(energy.Id, nav.LastFirstParameter);
    }

    // Scenario Outline: Selecting a create command invokes that action
    //   Given the command palette is open
    //   When I choose "<command>"
    //   Then the "<action>" is invoked
    [Theory]
    [InlineData("New project", "create a new project")]
    [InlineData("New conversation", "create a new conversation")]
    [InlineData("New artifact", "create a new artifact")]
    [InlineData("Search", "open search")]
    public void Selecting_a_create_command_invokes_that_action(string command, string action)
    {
        var vm = NewPalette(out var actions, out _);

        var target = vm.Results.Single(c => c.DisplayName == command);
        vm.ChooseCommand.Execute(target);

        Assert.Equal(new[] { action }, actions.Invoked.ToArray());
    }

    // Scenario: A query with no matches shows a designed empty result state
    //   When I type a query that matches nothing
    //   Then I see a "No matching commands" empty state
    //   And no raw error is shown
    [Fact]
    public void A_query_with_no_matches_shows_a_designed_empty_state()
    {
        var vm = NewPalette(out _, out _);

        vm.Query = "zzqqxx-nothing-matches";

        Assert.Empty(vm.Results);
        Assert.True(vm.IsEmptyState);
        Assert.Equal("No matching commands", vm.EmptyStateMessage);
    }

    private static CommandPaletteViewModel NewPalette(
        out RecordingCommandActions actions,
        out RecordingParamNavigationService navigation,
        FakeProjectService? projects = null,
        RecordingParamNavigationService? nav = null)
    {
        actions = new RecordingCommandActions();
        navigation = nav ?? new RecordingParamNavigationService();
        var registry = new CommandRegistry(projects ?? new FakeProjectService(), navigation, actions);
        return new CommandPaletteViewModel(registry);
    }

    private sealed class RecordingCommandActions : ICommandActions
    {
        public List<string> Invoked { get; } = new();
        public void NewProject() => Invoked.Add("create a new project");
        public void NewConversation() => Invoked.Add("create a new conversation");
        public void NewArtifact() => Invoked.Add("create a new artifact");
        public void OpenSearch() => Invoked.Add("open search");
    }

    private sealed class RecordingParamNavigationService : INavigationService
    {
        public Type? LastNavigatedTo { get; private set; }
        public object? LastFirstParameter { get; private set; }

        public ViewModelBase? CurrentViewModel => null;
        public string? ActiveProjectId => null;
        public bool CanGoBack => false;
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }

        public TViewModel NavigateTo<TViewModel>(params object[] parameters) where TViewModel : ViewModelBase
        {
            LastNavigatedTo = typeof(TViewModel);
            LastFirstParameter = parameters.Length > 0 ? parameters[0] : null;
            return null!;
        }

        public void Back()
        {
        }
    }
}
