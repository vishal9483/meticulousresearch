using CommunityToolkit.Mvvm.Input;
using MeticulousResearch.App.ViewModels;
using MeticulousResearch.Core.ViewStates;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the shared view-state machine (docs/features/empty-loading-error-states/tests.md
/// — Empty states, Loading states, and the recovery-action scenario). These prove the
/// Loading → Content / Empty / Error transitions and the working recovery action without a window
/// (SPEC §3.7).
/// </summary>
public class ViewStateViewModelTests
{
    /// <summary>Minimal concrete list view-model over the shared base, for state-machine tests.</summary>
    private sealed class TestListViewModel : ListViewModel<int>
    {
        public int CallToActionInvocations { get; private set; }

        public TestListViewModel(IUserErrorMapper mapper)
            : base(mapper)
        {
            CallToActionCommand = new RelayCommand(() => CallToActionInvocations++);
        }

        public override string CallToActionLabel => "New item";

        public override IRelayCommand CallToActionCommand { get; }

        public Task LoadWith(Func<Task<IReadOnlyList<int>>> loader) => LoadAsync(loader);
    }

    private static TestListViewModel NewVm() => new(new UserErrorMapper());

    // Scenario: A list view-model exposes an empty state when its collection is empty
    //   Given a list view-model with zero items
    //   Then its state is "Empty"
    //   And it exposes a non-empty call-to-action command
    [Fact]
    public void Empty_collection_yields_empty_state_with_a_call_to_action()
    {
        var vm = NewVm();

        Assert.Empty(vm.Items);
        Assert.Equal(ViewState.Empty, vm.State);
        Assert.NotNull(vm.CallToActionCommand);
        Assert.False(string.IsNullOrWhiteSpace(vm.CallToActionLabel));
        // The call-to-action is genuinely wired (invokable), not a null placeholder.
        Assert.True(vm.CallToActionCommand.CanExecute(null));
        vm.CallToActionCommand.Execute(null);
        Assert.Equal(1, vm.CallToActionInvocations);
    }

    // Scenario: Adding the first item leaves the empty state
    //   Given a list view-model in the "Empty" state
    //   When an item is added
    //   Then its state is "Content"
    [Fact]
    public void Adding_the_first_item_leaves_the_empty_state()
    {
        var vm = NewVm();
        Assert.Equal(ViewState.Empty, vm.State);

        vm.Items.Add(42);

        Assert.Equal(ViewState.Content, vm.State);
    }

    // Scenario: A view-model reports Loading while an async operation is in flight
    //   Given a view-model whose data load has not completed
    //   Then its state is "Loading"
    [Fact]
    public void Reports_loading_while_an_async_load_is_in_flight()
    {
        var vm = NewVm();
        var gate = new TaskCompletionSource<IReadOnlyList<int>>();

        var loading = vm.LoadWith(() => gate.Task);

        Assert.False(loading.IsCompleted);
        Assert.Equal(ViewState.Loading, vm.State);

        // Let the load complete so no task is left dangling.
        gate.SetResult(new[] { 1 });
        loading.GetAwaiter().GetResult();
    }

    // Scenario: A view-model leaves Loading when its data arrives
    //   Given a view-model in the "Loading" state
    //   When the data load completes with items
    //   Then its state is "Content"
    [Fact]
    public void Leaves_loading_for_content_when_data_arrives()
    {
        var vm = NewVm();
        var gate = new TaskCompletionSource<IReadOnlyList<int>>();
        var loading = vm.LoadWith(() => gate.Task);
        Assert.Equal(ViewState.Loading, vm.State);

        gate.SetResult(new[] { 1, 2, 3 });
        loading.GetAwaiter().GetResult();

        Assert.Equal(ViewState.Content, vm.State);
        Assert.Equal(new[] { 1, 2, 3 }, vm.Items);
    }

    // Scenario: The recovery action re-runs the failed operation
    //   Given a view in an error state with a "Retry" recovery action
    //   When I invoke the recovery action
    //   Then the failed operation is attempted again
    [Fact]
    public void Recovery_action_re_runs_the_failed_operation()
    {
        var vm = NewVm();
        var attempts = 0;

        Task<IReadOnlyList<int>> FailingLoad()
        {
            attempts++;
            throw new UserFacingException(UserFacingFailureKind.Offline);
        }

        vm.LoadWith(FailingLoad).GetAwaiter().GetResult();

        Assert.Equal(ViewState.Error, vm.State);
        Assert.NotNull(vm.Error);
        Assert.Equal("Retry", vm.Error!.RecoveryAction);
        Assert.Equal(1, attempts);
        Assert.True(vm.RecoveryCommand.CanExecute(null));

        vm.RecoveryCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        // The failed operation was attempted again.
        Assert.Equal(2, attempts);
    }
}
