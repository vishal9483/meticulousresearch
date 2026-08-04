using System.Linq;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Templates;
using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-10 — Generate a Market Research Report artifact from a template, iterate, and diff
/// (covers SPEC §9.1: 5). Template generation, Edit-with-Claude iteration (immutable versions),
/// diff, and revert all run headlessly over the real template / artifact / edit / diff services.
/// </summary>
public sealed class J10_TemplateReport : IDisposable
{
    private readonly JourneyHarness _h = new();
    private readonly string _projectId;

    public J10_TemplateReport()
    {
        _projectId = _h.Projects.Create("EV Market 2026", customInstructions: "Cite sources").Id;
        _h.Resources.AddText(_projectId, "Filing", "EV demand grows through 2030.");
    }

    public void Dispose() => _h.Dispose();

    private DeliverableTemplate MarketResearchTemplate() =>
        _h.TemplateCatalog.Templates.FirstOrDefault(t => t.Name.Contains("Market Research", StringComparison.OrdinalIgnoreCase))
        ?? _h.TemplateCatalog.Templates.First();

    // @e2e
    // Scenario: Ana produces and refines a template-driven report artifact
    [Fact]
    public async Task Ana_produces_and_refines_a_template_driven_report_artifact()
    {
        var template = MarketResearchTemplate();

        // When I generate from the template with scope/horizon/region.
        _h.Chat.WithCompletionText("# Report\n## Executive Summary\nThe market grows.\n## Findings\n- A")
            .WithUsage(1_000, 400);
        var artifact = await _h.Templates.GenerateFromTemplate(
            _projectId, template.Id, new TemplatePromptParameters(Scope: "EV", Horizon: "2030", Region: "NA"));

        // Then version 1 records model, prompt, in-scope resources, and usage.
        var v1 = _h.Artifacts.GetHistory(artifact.Id).Single();
        Assert.False(string.IsNullOrEmpty(v1.Model));
        Assert.False(string.IsNullOrEmpty(v1.Prompt));
        Assert.NotNull(v1.ResourceScopeJson);
        Assert.True(v1.TokensIn > 0 || v1.TokensOut > 0);

        // When I use "Edit with Claude" to tighten the executive summary.
        _h.Chat.WithCompletionText("# Report\n## Executive Summary\nTighter.\n## Findings\n- A").WithUsage(300, 120);
        var v2 = await _h.EditWithClaude.EditWithClaude(artifact.Id, "tighten the executive summary", "claude-opus-5");

        // Then version 2 is created immutably and version 1 is preserved.
        var history = _h.Artifacts.GetHistory(artifact.Id);
        Assert.Equal(2, history.Count);
        Assert.NotEqual(v1.Content, v2.Content);
        Assert.Equal(v1.Content, history.Single(v => v.Id == v1.Id).Content); // v1 unchanged

        // When I open diff mode and compare v1 and v2, the changes are shown.
        var diff = _h.Diff.Diff(v1, v2);
        Assert.True(diff.HasChanges);

        // When I revert to version 1, the current version becomes v1's content without destroying v2.
        _h.Artifacts.RevertTo(artifact.Id, v1.Id);
        var current = _h.Artifacts.GetHistory(artifact.Id).Single(v => v.Id == _h.Artifacts.Get(artifact.Id)!.CurrentVersionId);
        Assert.Equal(v1.Content, current.Content);
        Assert.Contains(_h.Artifacts.GetHistory(artifact.Id), v => v.Id == v2.Id); // v2 still retrievable
    }

    // @e2e @unit
    // Scenario: Manual edits and Claude edits both create immutable versions
    [Fact]
    public async Task Manual_edits_and_claude_edits_both_create_immutable_versions()
    {
        // Given an artifact at version 1.
        var artifact = _h.Artifacts.CreateFromContent(
            _projectId, ArtifactTypes.Doc, "Market Sizing", "# v1", contentFormat: null, ArtifactProvenance.User());

        // When I make a manual edit, version 2 is created with created_by = user and zero generation usage.
        _h.Artifacts.SetContent(artifact.Id, "# v2 (manual)");
        var manual = _h.Artifacts.GetHistory(artifact.Id).OrderBy(v => v.VersionNo).Last();
        Assert.Equal(ArtifactProvenance.CreatedByUser, manual.CreatedBy);
        Assert.Equal(0, manual.TokensIn);
        Assert.Equal(0, manual.TokensOut);

        // When I run an "Edit with Claude" follow-up, version 3 is created with created_by = claude and usage.
        _h.Chat.WithCompletionText("# v3 (claude)").WithUsage(150, 60);
        var claude = await _h.EditWithClaude.EditWithClaude(artifact.Id, "improve it", "claude-opus-5");
        Assert.Equal(ArtifactProvenance.CreatedByClaude, claude.CreatedBy);
        Assert.True(claude.TokensIn > 0 || claude.TokensOut > 0);
        Assert.Equal(3, _h.Artifacts.GetHistory(artifact.Id).Count);
    }
}
