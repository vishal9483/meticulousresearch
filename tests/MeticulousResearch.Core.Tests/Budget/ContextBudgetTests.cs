using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Budget;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Budget;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/context-budget/tests.md
/// (SPEC §3.2, §8): the before-send estimate (enabled + overhead, exclude disabled, include image
/// tokens), the window/budget threshold checks, and the model-switch re-resolution. A real
/// <see cref="ResourceService"/> over a temp store backs <see cref="ContextBudgetService"/>, and
/// resources are seeded with explicit token estimates so the totals are proven exactly. The
/// configured budget is a real persisted <see cref="SettingsService"/> value.
/// Background: a project with enabled resources whose token estimates sum to a known total, a
/// selected model whose window comes from the catalog, and a configured budget.
/// </summary>
public sealed class ContextBudgetTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DataStore _store;
    private readonly ResourceService _resources;
    private readonly SettingsService _settings;
    private readonly ContextBudgetService _budget;
    private readonly string _projectId;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));

    public ContextBudgetTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-context-budget-tests", Guid.NewGuid().ToString("N"));
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

    // Scenario: The pre-send estimate sums enabled resources plus fixed overhead
    [Fact]
    public void The_pre_send_estimate_sums_enabled_resources_plus_fixed_overhead()
    {
        // enabled resources estimated at 1,000 and 2,000 tokens
        Seed("A", tokens: 1_000, enabled: true);
        Seed("B", tokens: 2_000, enabled: true);

        // custom instructions and message overhead estimated at 500 tokens
        var scope = new ContextBudgetScope(OverheadTokens: 500);

        var estimate = _budget.Estimate(_projectId, scope, Model(200_000));

        // the estimated total is 3,500 tokens
        Assert.Equal(3_500, estimate.TotalTokens);
        // and it is labeled "estimated"
        Assert.Equal("estimated", estimate.Label);
    }

    // Scenario: Disabled resources are excluded from the estimate
    [Fact]
    public void Disabled_resources_are_excluded_from_the_estimate()
    {
        // enabled resources totaling 3,000 tokens
        Seed("A", tokens: 1_000, enabled: true);
        Seed("B", tokens: 2_000, enabled: true);
        // and a disabled resource of 5,000 tokens
        var disabled = Seed("Big", tokens: 5_000, enabled: false);

        var estimate = _budget.Estimate(_projectId, ContextBudgetScope.None, Model(200_000));

        // the disabled resource is not counted
        Assert.DoesNotContain(estimate.Contributions, c => c.ResourceId == disabled);
        Assert.Equal(3_000, estimate.TotalTokens);
    }

    // Scenario: Image resources contribute their estimated image tokens
    [Fact]
    public void Image_resources_contribute_their_estimated_image_tokens()
    {
        // an enabled image resource contributing an estimated image-token amount
        var imageTokens = new HeuristicTokenEstimator().EstimateImageTokens(1024, 768);
        Assert.True(imageTokens > 0);
        var imageId = Seed("Chart", tokens: imageTokens, enabled: true, type: ResourceTypes.Image);

        var estimate = _budget.Estimate(_projectId, ContextBudgetScope.None, Model(200_000));

        // the image tokens are included in the total
        Assert.Contains(estimate.Contributions, c => c.ResourceId == imageId && c.Tokens == imageTokens);
        Assert.Equal(imageTokens, estimate.TotalTokens);
    }

    // Scenario: The estimate is checked against the selected model's context window
    [Fact]
    public void The_estimate_is_checked_against_the_selected_models_context_window()
    {
        // an estimated total of 150,000 tokens
        Seed("A", tokens: 150_000, enabled: true);
        // a configured budget that does not itself trip (so only the window is under test)
        _settings.ContextBudget = 200_000;

        // a model with a 200,000-token context window
        var estimate = _budget.Estimate(_projectId, ContextBudgetScope.None, Model(200_000));

        // the estimate is within the model window
        Assert.Equal(ContextBudgetStatus.Ok, estimate.Status);
        // and no warning is shown
        Assert.False(estimate.HasWarning);
    }

    // Scenario Outline: Exceeding the window or the configured budget warns
    [Theory]
    [InlineData(200_000, 100_000, 90_000, "none")]
    [InlineData(200_000, 100_000, 120_000, "over configured budget")]
    [InlineData(200_000, 250_000, 210_000, "over model context window")]
    [InlineData(200_000, 100_000, 260_000, "over model context window")]
    public void Exceeding_the_window_or_the_configured_budget_warns(long window, int budget, long total, string warn)
    {
        // a model window of <window> tokens and a configured budget of <budget> tokens
        _settings.ContextBudget = budget;
        // an estimated total of <total> tokens
        Seed("A", tokens: total, enabled: true);

        var estimate = _budget.Estimate(_projectId, ContextBudgetScope.None, Model(window));

        // a warning "<warn>" is shown
        Assert.Equal(warn, estimate.WarningMessage);
        if (warn == "none")
            Assert.False(estimate.HasWarning);
        else
            Assert.True(estimate.HasWarning);
    }

    // Scenario: Switching to a larger-window model clears an over-window warning
    [Fact]
    public void Switching_to_a_larger_window_model_clears_an_over_window_warning()
    {
        // an estimate that exceeds a 200,000-token model window
        _settings.ContextBudget = 100_000;
        Seed("A", tokens: 260_000, enabled: true);

        var before = _budget.Estimate(_projectId, ContextBudgetScope.None, Model(200_000));
        Assert.Equal(ContextBudgetStatus.OverWindow, before.Status);
        Assert.Equal("over model context window", before.WarningMessage);

        // I switch to a 1,000,000-token model
        var after = _budget.Estimate(_projectId, ContextBudgetScope.None, Model(1_000_000));

        // the over-window warning clears
        Assert.NotEqual(ContextBudgetStatus.OverWindow, after.Status);
        Assert.NotEqual("over model context window", after.WarningMessage);
    }

    private ModelWindow Model(long contextTokens) => new("claude-test", contextTokens);

    private string Seed(string title, long tokens, bool enabled, string type = "text")
    {
        var id = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Resources.Add(new Resource
        {
            Id = id,
            ProjectId = _projectId,
            Title = title,
            Type = type,
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
