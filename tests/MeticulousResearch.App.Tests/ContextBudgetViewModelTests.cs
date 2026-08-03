using Microsoft.Data.Sqlite;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Budget;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// Faithful @unit translation of the "help deselect — no silent truncation" scenarios in
/// docs/features/context-budget/tests.md (SPEC §3.2, §8), exercised through the composer budget
/// meter view-model. Window-free: a real <see cref="ResourceService"/> over a temp store backs the
/// real <see cref="ContextBudgetService"/>, so deselecting a resource genuinely leaves the enabled
/// scope and the meter recomputes live. Content is never dropped or truncated to fit — the meter
/// only refuses generation until the user deselects or switches model.
/// </summary>
public sealed class ContextBudgetViewModelTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DataStore _store;
    private readonly ResourceService _resources;
    private readonly SettingsService _settings;
    private readonly ContextBudgetService _budget;
    private readonly string _projectId;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));

    public ContextBudgetViewModelTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-context-budget-vm-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _settings = new SettingsService(_store);
        _budget = new ContextBudgetService(_resources, _settings);
        _projectId = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Semiconductors 2026",
            Archived = false,
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
    }

    // Scenario: When over budget, the app helps the user deselect resources
    [Fact]
    public void When_over_budget_the_app_helps_the_user_deselect_resources()
    {
        // an estimated total that exceeds the budget
        _settings.ContextBudget = 80_000;
        var largest = Seed("Big", tokens: 60_000, enabled: true);
        Seed("Mid", tokens: 30_000, enabled: true);
        Seed("Small", tokens: 10_000, enabled: true);
        var vm = NewVm(window: 200_000);
        Assert.True(vm.HasWarning);

        // I am shown which resources contribute most (largest-first)
        Assert.Equal(60_000, vm.Contributors[0].Tokens);
        Assert.Equal(largest, vm.Contributors[0].ResourceId);
        Assert.True(vm.Contributors[0].Tokens >= vm.Contributors[1].Tokens);
        Assert.True(vm.Contributors[1].Tokens >= vm.Contributors[2].Tokens);

        // I can deselect resources to bring the total under budget
        vm.DeselectCommand.Execute(largest);
        Assert.Equal(40_000, vm.EstimatedTotal);
        Assert.False(vm.HasWarning);
    }

    // Scenario: Deselecting resources recomputes the estimate live
    [Fact]
    public void Deselecting_resources_recomputes_the_estimate_live()
    {
        // an over-budget estimate
        _settings.ContextBudget = 80_000;
        var largest = Seed("Big", tokens: 60_000, enabled: true);
        Seed("Mid", tokens: 30_000, enabled: true);
        Seed("Small", tokens: 10_000, enabled: true);
        var vm = NewVm(window: 200_000);
        var before = vm.EstimatedTotal;
        Assert.True(vm.HasWarning);

        // I disable the largest resource
        vm.DeselectCommand.Execute(largest);

        // the estimated total decreases by that resource's estimate
        Assert.Equal(before - 60_000, vm.EstimatedTotal);
        // and the warning clears once the total is under budget
        Assert.False(vm.HasWarning);
    }

    // Scenario: Content is never silently truncated to fit
    [Fact]
    public void Content_is_never_silently_truncated_to_fit()
    {
        // an estimated total that exceeds the model window
        _settings.ContextBudget = 100_000;
        Seed("Huge", tokens: 200_000, enabled: true);
        var trimmable = Seed("Extra", tokens: 60_000, enabled: true);
        var vm = NewVm(window: 200_000);
        Assert.Equal(ContextBudgetStatus.OverWindow, vm.Status);

        var enabledBefore = _resources.ListEnabled(_projectId).Count;

        // a generation is attempted without resolving the overage
        var proceeded = vm.AttemptGeneration();

        // the app does not drop or truncate resources automatically
        Assert.False(proceeded);
        Assert.False(vm.CanGenerate);
        Assert.Equal(enabledBefore, _resources.ListEnabled(_projectId).Count);

        // and it requires the user to deselect ...
        vm.DeselectCommand.Execute(trimmable);
        Assert.True(vm.CanGenerate);
        Assert.True(vm.AttemptGeneration());
    }

    // Scenario: Content is never silently truncated to fit — resolving by switching model instead
    [Fact]
    public void An_over_window_estimate_can_be_resolved_by_switching_to_a_larger_window_model()
    {
        _settings.ContextBudget = 100_000;
        Seed("Huge", tokens: 260_000, enabled: true);
        var vm = NewVm(window: 200_000);
        Assert.False(vm.CanGenerate);

        // switch to a larger-window model rather than deselecting
        vm.SwitchModel(new ModelWindow("claude-large", 1_000_000));

        Assert.True(vm.CanGenerate);
        Assert.NotEqual(ContextBudgetStatus.OverWindow, vm.Status);
    }

    private ContextBudgetViewModel NewVm(long window) =>
        new(_projectId, _resources, _budget, new ModelWindow("claude-test", window), ContextBudgetScope.None);

    private string Seed(string title, long tokens, bool enabled)
    {
        var id = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Resources.Add(new Resource
        {
            Id = id,
            ProjectId = _projectId,
            Title = title,
            Type = "text",
            TokenEstimate = tokens,
            Enabled = enabled,
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
        return id;
    }

    public void Dispose()
    {
        _store.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
