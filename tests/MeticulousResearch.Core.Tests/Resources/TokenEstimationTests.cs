using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Resources.Extraction;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Resources;

/// <summary>
/// Faithful xUnit translation of the @unit scenarios in docs/features/token-estimation/tests.md
/// (SPEC §3.2, §3.6). The estimator scenarios exercise the pure <see cref="HeuristicTokenEstimator"/>;
/// the per-resource scenarios drive a temp SQLite store + file layout (allowed for @unit per
/// TESTING-STRATEGY §4) so they run in the headless gate.
/// </summary>
public sealed class TokenEstimationTests : IDisposable
{
    private readonly string _dataDir;
    private readonly string _fixtureDir;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly DataStore _store;
    private readonly ResourceService _service;
    private readonly string _projectId;

    public TokenEstimationTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-token-estimation-tests", Guid.NewGuid().ToString("N"));
        _fixtureDir = Path.Combine(_dataDir, "fixtures");
        Directory.CreateDirectory(_fixtureDir);
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var projects = new ProjectService(_store, new SettingsService(_store));
        _projectId = projects.Create("Semiconductors 2026").Id;
        _service = new ResourceService(_store, new HeuristicTokenEstimator());
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

    // Scenario: Estimating the same text twice yields the same number
    [Fact]
    public void Estimating_the_same_text_twice_yields_the_same_number()
    {
        // Given the text "..."
        const string text = "Global foundry capacity grew 12% in 2025.";
        var estimator = new HeuristicTokenEstimator();

        // When I estimate its tokens twice
        var first = estimator.Estimate(text);
        var second = estimator.Estimate(text);

        // Then both estimates are equal
        Assert.Equal(first, second);
    }

    // Scenario: The estimator runs locally with no network call
    [Fact]
    public void The_estimator_runs_locally_with_no_network_call()
    {
        // Given any input text
        const string text = "any input text for a purely local estimate";
        var type = typeof(HeuristicTokenEstimator);

        // Then no network or model API call is made: the estimator holds no network client, takes no
        // network dependency in its constructors, and Estimate is a synchronous non-Task method.
        Assert.Empty(type.GetConstructors().SelectMany(c => c.GetParameters()));

        var networkFields = type
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(System.Net.Http.HttpClient)
                        || f.FieldType.FullName?.Contains("HttpClient", StringComparison.Ordinal) == true
                        || f.FieldType.FullName?.Contains("Socket", StringComparison.Ordinal) == true);
        Assert.Empty(networkFields);

        var estimateMethod = type.GetMethod(nameof(HeuristicTokenEstimator.Estimate))!;
        Assert.False(typeof(System.Threading.Tasks.Task).IsAssignableFrom(estimateMethod.ReturnType));

        // When I estimate its tokens (completes synchronously, offline)
        var estimate = new HeuristicTokenEstimator().Estimate(text);
        Assert.True(estimate > 0);
    }

    // Scenario Outline: Longer inputs estimate to more tokens
    [Theory]
    [InlineData("ok", 1)]
    [InlineData("Global foundry capacity grew in 2025.", 5)]
    public void Longer_inputs_estimate_to_more_tokens(string text, long min)
    {
        // When I estimate its tokens
        var estimate = new HeuristicTokenEstimator().Estimate(text);

        // Then the estimate is at least <min>
        Assert.True(estimate >= min, $"Expected estimate >= {min} but was {estimate} for \"{text}\".");
    }

    // Scenario: Empty text estimates to zero tokens
    [Fact]
    public void Empty_text_estimates_to_zero_tokens()
    {
        // Given empty text / When I estimate its tokens
        var estimate = new HeuristicTokenEstimator().Estimate(string.Empty);

        // Then the estimate is 0
        Assert.Equal(0, estimate);
    }

    // Scenario: A resource's token estimate is derived from its extracted text
    [Fact]
    public void A_resources_token_estimate_is_derived_from_its_extracted_text()
    {
        // Given a text resource with extracted text of a known length
        const string text = "Wafer starts rose sharply across every leading-edge node in 2026.";
        var resource = _service.AddText(_projectId, "Wafer starts", text);

        // When its token estimate is computed / Then token_estimate equals the estimator's result
        var expected = new HeuristicTokenEstimator().Estimate(text);
        Assert.Equal(expected, _service.Get(resource.Id)!.TokenEstimate);
    }

    // Scenario: Image resources contribute an estimated image-token amount
    [Fact]
    public void Image_resources_contribute_an_estimated_image_token_amount()
    {
        // Given an image resource (represented by its pixel dimensions)
        var estimator = new HeuristicTokenEstimator();

        // When its token estimate is computed
        var imageTokens = estimator.EstimateImageTokens(1024, 768);

        // Then it contributes a non-zero image-token estimate toward context
        Assert.True(imageTokens > 0);
    }

    // Scenario: Re-extraction recomputes the token estimate
    [Fact]
    public void Re_extraction_recomputes_the_token_estimate()
    {
        // Given a resource whose extracted text changes on re-extract
        const string original = "Short note.";
        var path = FileFixtures.WritePlainText(_fixtureDir, "note", "txt", original);
        var added = _service.AddFile(_projectId, path);
        var resourceId = added.Resource.Id;

        const string updated = "A substantially longer note with far more content than before, changing the estimate.";
        File.WriteAllText(path, updated, new UTF8Encoding(false));
        // Also overwrite the stored blob so re-extraction reads the new content.
        File.WriteAllText(added.Resource.BlobPath!, updated, new UTF8Encoding(false));

        // When it is re-extracted
        var result = _service.ReExtract(resourceId);

        // Then its token_estimate is recomputed from the new text
        var expected = new HeuristicTokenEstimator().Estimate(updated);
        Assert.Equal(expected, _service.Get(resourceId)!.TokenEstimate);
        Assert.Equal(expected, result.Resource.TokenEstimate);
    }

    // Scenario: The estimator approximates model tokenization within a stated tolerance
    [Fact]
    public void The_estimator_approximates_model_tokenization_within_a_stated_tolerance()
    {
        // Given a reference text with a known approximate token count.
        // "The quick brown fox jumps over the lazy dog." tokenizes to ~10 tokens on real models.
        const string reference = "The quick brown fox jumps over the lazy dog.";
        const double knownApproxTokens = 10.0;

        // When I estimate its tokens
        var estimate = new HeuristicTokenEstimator().Estimate(reference);

        // Then the estimate is within the documented tolerance of the reference
        var maxDelta = knownApproxTokens * HeuristicTokenEstimator.DocumentedTolerance;
        Assert.InRange(estimate, knownApproxTokens - maxDelta, knownApproxTokens + maxDelta);
    }
}
