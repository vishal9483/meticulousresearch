using System.Globalization;
using System.Text;
using MeticulousResearch.Core.Cost;

namespace MeticulousResearch.Core.Export;

/// <summary>
/// The production <see cref="IUsageCsvExporter"/>: a deterministic, offline serializer over
/// <see cref="ICostService.GetPricedTurns"/> (SPEC §3.6). Emits the core column set
/// <c>timestamp,source,model,tokens_in,tokens_out,cost_usd</c>, one row per completed turn, ordered
/// ascending by timestamp, with RFC-4180 quoting/escaping and invariant-culture number formatting so
/// output is culture-independent and repeat exports are byte-identical.
/// </summary>
public sealed class UsageCsvExporter : IUsageCsvExporter
{
    /// <summary>The CSV header row (the core per-turn column set).</summary>
    public const string Header = "timestamp,source,model,tokens_in,tokens_out,cost_usd";

    // RFC-4180: CRLF record separator for a stable, deterministic newline.
    private const string Newline = "\r\n";

    private readonly ICostService _cost;

    /// <summary>Creates the exporter over the cost service that owns priced-turn records.</summary>
    /// <param name="cost">The cost service supplying per-turn priced records.</param>
    public UsageCsvExporter(ICostService cost)
        => _cost = cost ?? throw new ArgumentNullException(nameof(cost));

    /// <inheritdoc />
    public string Render(string projectId)
    {
        ArgumentNullException.ThrowIfNull(projectId);

        var rows = _cost.GetPricedTurns(projectId)
            .OrderBy(r => r.Timestamp)
            .ThenBy(r => r.TurnId, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.Append(Header).Append(Newline);
        foreach (var r in rows)
        {
            sb.Append(Field(r.Timestamp.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Field(SourceLabel(r.Source))).Append(',');
            sb.Append(Field(r.Model ?? string.Empty)).Append(',');
            sb.Append(Field(r.Usage.InputTokens.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Field(r.Usage.OutputTokens.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Field(FormatCost(r.Cost))).Append(Newline);
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public void Export(string projectId, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(projectId);
        ArgumentNullException.ThrowIfNull(destinationPath);

        // UTF-8 without BOM keeps repeat exports byte-identical and tool-friendly.
        File.WriteAllText(destinationPath, Render(projectId), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string SourceLabel(CostSource source)
        => source == CostSource.Artifact ? "artifact" : "conversation";

    // Unknown-price turns have no cost; leave the cell empty rather than inventing a $0.00.
    private static string FormatCost(decimal? cost)
        => cost is { } c ? c.ToString(CultureInfo.InvariantCulture) : string.Empty;

    // RFC-4180: quote a field containing a comma, double-quote, CR or LF; double embedded quotes.
    private static string Field(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
