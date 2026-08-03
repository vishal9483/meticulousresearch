namespace MeticulousResearch.Core.Export;

/// <summary>
/// Exports a project's usage/cost as a <b>per-turn CSV</b> (SPEC §3.6, §9.1(7)): one row per
/// completed turn (conversation turns and artifact generations) with the cost column computed from
/// stored tokens at <b>current</b> catalog prices via <c>cost-tracking</c>'s <c>ICostService</c>.
/// This is a pure serializer — it recomputes no cost of its own, makes no network call, and produces
/// byte-identical output for identical input under a fixed clock and price table.
/// </summary>
public interface IUsageCsvExporter
{
    /// <summary>
    /// Renders the project's usage as CSV text (header + one row per completed turn), without
    /// touching disk. Rows are ordered ascending by timestamp; a project with no completed turns
    /// yields a header-only CSV.
    /// </summary>
    /// <param name="projectId">The project whose usage to render.</param>
    /// <returns>The full CSV document as a single string.</returns>
    string Render(string projectId);

    /// <summary>
    /// Writes the project's usage CSV (see <see cref="Render"/>) to <paramref name="destinationPath"/>.
    /// Repeated exports of the same project under a fixed clock and price table are byte-identical.
    /// </summary>
    /// <param name="projectId">The project whose usage to export.</param>
    /// <param name="destinationPath">The filesystem path to write the CSV to.</param>
    void Export(string projectId, string destinationPath);
}
