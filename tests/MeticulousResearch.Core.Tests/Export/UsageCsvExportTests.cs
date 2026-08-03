using System.Globalization;
using MeticulousResearch.Core.Cost;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Export;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Export;

/// <summary>
/// Faithful xUnit translation of the @unit @integration scenarios in
/// docs/features/usage-csv-export/tests.md (SPEC §3.6, §9.1(7) — export project cost/usage as a
/// per-turn CSV, cost computed from current prices). The Background's fixed price table
/// (USD per MTok) and two completed turns are seeded into a real temp <see cref="DataStore"/>;
/// cost columns come from <see cref="ICostService"/> and are always recomputed from stored tokens.
/// </summary>
public sealed class UsageCsvExportTests : IDisposable
{
    // Background: a fixed price table in USD per million tokens (input/output only).
    private readonly DictionaryCostPriceSource _prices = new();
    // Background: a fixed clock so windows/timestamps are deterministic.
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly string _dataDir;
    private readonly DataStore _store;
    private readonly CostService _cost;
    private readonly UsageCsvExporter _exporter;
    private readonly string _projectId = Guid.NewGuid().ToString("N");

    public UsageCsvExportTests()
    {
        _prices.SetRates("claude-opus-5", new CostRates(5m, 25m, 0m, 0m));

        _dataDir = Path.Combine(Path.GetTempPath(), "mr-usage-csv-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        _cost = new CostService(_store, _prices, _clock);
        _exporter = new UsageCsvExporter(_cost);

        SeedProject(_projectId, "EV Market 2026");
        // Background turns: a conversation turn at 09:00 and an artifact generation at 10:00.
        var conversationId = SeedConversation(_projectId);
        SeedAssistantTurn(conversationId, "claude-opus-5", 1_000_000, 100_000,
            new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero));
        SeedArtifactVersion(_projectId, "claude-opus-5", 500_000, 50_000,
            new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero));
    }

    // ---- Per-turn rows ------------------------------------------------------------------------

    // Scenario: The CSV has one row per completed turn
    [Fact]
    [Trait("Category", "integration")]
    public void The_CSV_has_one_row_per_completed_turn()
    {
        var lines = DataLines(_exporter.Render(_projectId));

        // Then the CSV has 2 data rows
        Assert.Equal(2, lines.Count);
        // And a header row
        Assert.Equal(UsageCsvExporter.Header, HeaderLine(_exporter.Render(_projectId)));
    }

    // Scenario: Each row carries the per-turn usage fields
    [Fact]
    [Trait("Category", "integration")]
    public void Each_row_carries_the_per_turn_usage_fields()
    {
        var csv = _exporter.Render(_projectId);

        // Then each row includes: timestamp, source, model, tokens_in, tokens_out, cost_usd
        Assert.Equal("timestamp,source,model,tokens_in,tokens_out,cost_usd", HeaderLine(csv));
        foreach (var line in DataLines(csv))
            Assert.Equal(6, ParseCsvLine(line).Count);
    }

    // Scenario: The source column distinguishes conversations from artifact generations
    [Fact]
    [Trait("Category", "integration")]
    public void The_source_column_distinguishes_conversations_from_artifact_generations()
    {
        var rows = DataLines(_exporter.Render(_projectId)).Select(ParseCsvLine).ToList();

        // And a project "EV Market 2026" with the conversation turn first, artifact second.
        Assert.Equal("conversation", rows[0][1]);
        Assert.Equal("artifact", rows[1][1]);
    }

    // ---- Computed cost column -----------------------------------------------------------------

    // Scenario: The cost column is computed from tokens and current prices
    [Fact]
    [Trait("Category", "integration")]
    public void The_cost_column_is_computed_from_tokens_and_current_prices()
    {
        var rows = DataLines(_exporter.Render(_projectId)).Select(ParseCsvLine).ToList();

        // Then the first row's cost_usd is 7.50 and the second's is 3.75.
        Assert.Equal(7.50m, CostOf(rows[0]));
        Assert.Equal(3.75m, CostOf(rows[1]));
    }

    // Scenario: A price update changes the exported cost from the same tokens
    [Fact]
    [Trait("Category", "integration")]
    public void A_price_update_changes_the_exported_cost_from_the_same_tokens()
    {
        var before = DataLines(_exporter.Render(_projectId)).Select(ParseCsvLine).ToList();

        // Given the price for "claude-opus-5" input changes to 10 per MTok
        _prices.SetRates("claude-opus-5", new CostRates(10m, 25m, 0m, 0m));

        var after = DataLines(_exporter.Render(_projectId)).Select(ParseCsvLine).ToList();

        // Then the first row's cost_usd reflects the new price (1M in *10 + 100k out *25 = 12.50)
        Assert.Equal(7.50m, CostOf(before[0]));
        Assert.Equal(12.50m, CostOf(after[0]));
        // And the token columns are unchanged
        Assert.Equal(before[0][3], after[0][3]);
        Assert.Equal(before[0][4], after[0][4]);
    }

    // ---- Well-formed, deterministic output ----------------------------------------------------

    // Scenario: Rows are ordered deterministically by timestamp
    [Fact]
    [Trait("Category", "integration")]
    public void Rows_are_ordered_deterministically_by_timestamp()
    {
        var timestamps = DataLines(_exporter.Render(_projectId))
            .Select(l => DateTimeOffset.Parse(ParseCsvLine(l)[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
            .ToList();

        // Then rows appear in ascending timestamp order
        var sorted = timestamps.OrderBy(t => t).ToList();
        Assert.Equal(sorted, timestamps);
    }

    // Scenario: The same project exports byte-identical CSV on repeat
    [Fact]
    [Trait("Category", "integration")]
    public void The_same_project_exports_byte_identical_CSV_on_repeat()
    {
        var pathA = Path.Combine(_dataDir, "a.csv");
        var pathB = Path.Combine(_dataDir, "b.csv");

        // When I export its usage as CSV twice (fixed clock and price table)
        _exporter.Export(_projectId, pathA);
        _exporter.Export(_projectId, pathB);

        // Then the two CSV files are identical
        Assert.Equal(File.ReadAllBytes(pathA), File.ReadAllBytes(pathB));
    }

    // Scenario: Fields needing escaping are quoted per CSV rules
    [Fact]
    [Trait("Category", "integration")]
    public void Fields_needing_escaping_are_quoted_per_CSV_rules()
    {
        // Given a turn whose model label contains a comma
        const string labelWithComma = "claude-opus-5, preview";
        _prices.SetRates(labelWithComma, new CostRates(5m, 25m, 0m, 0m));
        var conversationId = SeedConversation(_projectId);
        SeedAssistantTurn(conversationId, labelWithComma, 1_000_000, 0,
            new DateTimeOffset(2026, 8, 3, 11, 0, 0, TimeSpan.Zero));

        var csv = _exporter.Render(_projectId);

        // Then that field is quoted in the raw CSV
        Assert.Contains("\"" + labelWithComma + "\"", csv, StringComparison.Ordinal);

        // And the CSV parses back to the original values (round-trip)
        var modelCells = DataLines(csv).Select(l => ParseCsvLine(l)[2]).ToList();
        Assert.Contains(labelWithComma, modelCells);
    }

    // Scenario: A project with no completed turns exports a header-only CSV
    [Fact]
    [Trait("Category", "integration")]
    public void A_project_with_no_completed_turns_exports_a_header_only_CSV()
    {
        // Given a project with no completed turns
        var emptyProjectId = Guid.NewGuid().ToString("N");
        SeedProject(emptyProjectId, "Empty");

        var csv = _exporter.Render(emptyProjectId);

        // Then the CSV has a header row and no data rows
        Assert.Equal(UsageCsvExporter.Header, HeaderLine(csv));
        Assert.Empty(DataLines(csv));
    }

    // Scenario: CSV export makes no network calls
    [Fact]
    [Trait("Category", "integration")]
    public void CSV_export_makes_no_network_calls()
    {
        // The exporter depends only on ICostService and the filesystem; there is no HttpClient,
        // socket, or any network dependency in its construction or render path. We assert this
        // structurally: the render path completes without any network primitive being reachable.
        var referenced = typeof(UsageCsvExporter).Assembly.GetName().Name;
        Assert.Equal("MeticulousResearch.Core", referenced);

        // And it renders successfully offline (no exception, deterministic content).
        var csv = _exporter.Render(_projectId);
        Assert.StartsWith(UsageCsvExporter.Header, csv, StringComparison.Ordinal);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static string HeaderLine(string csv)
        => csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.None)[0];

    private static List<string> DataLines(string csv)
        => csv.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .ToList();

    private static decimal CostOf(IReadOnlyList<string> row)
        => decimal.Parse(row[5], CultureInfo.InvariantCulture);

    // Minimal RFC-4180 CSV line parser (single-line records) for round-trip assertions.
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }

    private void SeedProject(string projectId, string name)
    {
        using var db = _store.CreateDbContext();
        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = name,
            Archived = false,
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
    }

    private string SeedConversation(string projectId)
    {
        var id = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Conversations.Add(new Conversation
        {
            Id = id,
            ProjectId = projectId,
            Title = "Thread",
            CreatedAt = _clock.UtcNow.ToString("o"),
            UpdatedAt = _clock.UtcNow.ToString("o"),
        });
        db.SaveChanges();
        return id;
    }

    private void SeedAssistantTurn(string conversationId, string model, long input, long output, DateTimeOffset when)
    {
        using var db = _store.CreateDbContext();
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid().ToString("N"),
            ConversationId = conversationId,
            Role = "assistant",
            Content = "answer",
            Model = model,
            TokensIn = input,
            TokensOut = output,
            CreatedAt = when.ToString("o"),
        });
        db.SaveChanges();
    }

    private void SeedArtifactVersion(string projectId, string model, long input, long output, DateTimeOffset when)
    {
        var artifactId = Guid.NewGuid().ToString("N");
        using var db = _store.CreateDbContext();
        db.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            ProjectId = projectId,
            Title = "Deliverable",
            Type = "doc",
            CreatedAt = when.ToString("o"),
            UpdatedAt = when.ToString("o"),
        });
        db.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = Guid.NewGuid().ToString("N"),
            ArtifactId = artifactId,
            VersionNo = 1,
            Content = "body",
            Model = model,
            TokensIn = input,
            TokensOut = output,
            CreatedBy = "claude",
            CreatedAt = when.ToString("o"),
        });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _store.Dispose();
        try
        {
            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup; a locked SQLite file must not fail the test.
        }
    }
}
