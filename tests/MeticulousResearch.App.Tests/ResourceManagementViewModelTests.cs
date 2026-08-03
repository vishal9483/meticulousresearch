using Microsoft.Data.Sqlite;
using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit tests for the token-estimate-contribution scenarios in
/// docs/features/resource-management/tests.md. Window-free: a real <see cref="ResourceService"/>
/// over a temp store backs the Resources view-model, and resources are seeded with explicit token
/// estimates so the per-row contribution and the enabled-scope total are proven exactly.
/// </summary>
public sealed class ResourceManagementViewModelTests : IDisposable
{
    private readonly string _dataDir;
    private readonly DataStore _store;
    private readonly ResourceService _service;
    private readonly string _projectId;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));

    public ResourceManagementViewModelTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-resource-mgmt-vm-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        _service = new ResourceService(_store, new HeuristicTokenEstimator());
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
            // Best-effort temp cleanup.
        }
    }

    // Seed a resource row directly with an explicit token estimate and enabled flag so the tests
    // control the exact numbers the Gherkin cites.
    private void SeedResource(string title, long tokenEstimate, bool enabled, string createdAt)
    {
        using var db = _store.CreateDbContext();
        db.Resources.Add(new Resource
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = _projectId,
            Title = title,
            Type = ResourceTypes.Text,
            TokenEstimate = tokenEstimate,
            ByteSize = tokenEstimate,
            Enabled = enabled,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });
        db.SaveChanges();
    }

    // Scenario: Each resource shows its own token-estimate contribution
    [Fact]
    public void Each_resource_shows_its_own_token_estimate_contribution()
    {
        // Given resources with token estimates 100, 250, and 400
        SeedResource("A", 100, enabled: true, "2026-08-03T12:00:01Z");
        SeedResource("B", 250, enabled: true, "2026-08-03T12:00:02Z");
        SeedResource("C", 400, enabled: true, "2026-08-03T12:00:03Z");

        // When I view the resources table
        var vm = new ResourcesViewModel(_projectId, _service);

        // Then each row shows its token estimate
        var estimates = vm.Resources.Select(r => r.TokenEstimate).OrderBy(x => x).ToArray();
        Assert.Equal(new long[] { 100, 250, 400 }, estimates);
    }

    // Scenario: The resources view shows the total estimate for enabled resources
    [Fact]
    public void The_view_shows_the_total_estimate_for_enabled_resources()
    {
        // Given enabled resources estimated at 100 and 250 and a disabled one at 400
        SeedResource("A", 100, enabled: true, "2026-08-03T12:00:01Z");
        SeedResource("B", 250, enabled: true, "2026-08-03T12:00:02Z");
        SeedResource("C", 400, enabled: false, "2026-08-03T12:00:03Z");

        // When I view the resources total
        var vm = new ResourcesViewModel(_projectId, _service);

        // Then the enabled-scope total is 350
        Assert.Equal(350, vm.EnabledTokenTotal);

        // And the disabled resource is excluded from the total
        Assert.NotEqual(750, vm.EnabledTokenTotal);
    }
}
