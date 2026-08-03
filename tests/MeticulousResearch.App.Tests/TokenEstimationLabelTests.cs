using MeticulousResearch.App.ViewModels.Sections;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.App.Tests;

/// <summary>
/// @unit test for the labeling scenario in docs/features/token-estimation/tests.md (SPEC §3.6):
/// every surfaced token estimate must be marked "estimated", distinct from an authoritative API
/// usage count. Window-free: projects a <see cref="Resource"/> into its display row and asserts the
/// surfaced label carries the "estimated" marker.
/// </summary>
public sealed class TokenEstimationLabelTests
{
    // Scenario: Estimates are surfaced with an "estimated" label
    [Fact]
    public void Estimates_are_surfaced_with_an_estimated_label()
    {
        // Given a resource token estimate
        var resource = new Resource
        {
            Id = "r1",
            ProjectId = "p1",
            Title = "Wafer starts",
            Type = ResourceTypes.Text,
            TokenEstimate = 128,
            ByteSize = 512,
            Enabled = true,
            CreatedAt = "2026-08-03T12:00:00Z",
            UpdatedAt = "2026-08-03T12:00:00Z",
        };

        // When it is shown in the UI
        var row = new ResourceRowViewModel(resource);

        // Then it is labeled as "estimated" (not an authoritative count)
        Assert.Contains("estimated", row.TokenEstimateLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("128", row.TokenEstimateLabel);
    }
}
