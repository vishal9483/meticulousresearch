using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;
using MeticulousResearch.Core.Settings;
using MeticulousResearch.Core.Templates;
using MeticulousResearch.Core.Tests.Turns;
using MeticulousResearch.TestSupport;

namespace MeticulousResearch.Core.Tests.Templates;

/// <summary>
/// Faithful xUnit translation of every <c>@unit</c> scenario in
/// docs/features/deliverable-templates/tests.md (SPEC §3.4.1, §6.3). None carry an excluded
/// <c>Category</c> trait, so they run in the headless gate over the real config-driven catalog and a
/// real <see cref="ArtifactService"/>/<see cref="TemplateService"/> backed by a temp SQLite store; AI
/// generation is served by the deterministic <see cref="FakeChatService"/> (TESTING-STRATEGY §4).
///
/// Background: the default deliverable-template catalog is loaded; a project "Grid Storage 2026" with
/// 2 enabled resources; AI generation served by a deterministic FakeChatService.
/// </summary>
public sealed class DeliverableTemplatesTests : IDisposable
{
    private readonly string _dataDir;
    private readonly AdvancingClock _clock =
        new(new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero), TimeSpan.FromMilliseconds(5));
    private readonly DataStore _store;
    private readonly ProjectService _projects;
    private readonly ResourceService _resources;
    private readonly FakeChatService _chat = new();
    private readonly ArtifactService _artifacts;
    private readonly IModelCatalog _models = ModelCatalogLoader.Default;
    private readonly ITemplateCatalog _catalog = TemplateCatalogLoader.Default;
    private readonly TemplateService _templates;
    private readonly string _projectId;
    private readonly Resource _resourceA;
    private readonly Resource _resourceB;

    public DeliverableTemplatesTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "mr-template-tests", Guid.NewGuid().ToString("N"));
        _store = new DataStore(_clock, _dataDir);
        _store.Initialize();
        var settings = new SettingsService(_store);
        _projects = new ProjectService(_store, settings);
        _resources = new ResourceService(_store, new HeuristicTokenEstimator());
        _artifacts = new ArtifactService(_store, _chat, _clock);
        _templates = new TemplateService(_catalog, _artifacts, _resources, _models, _projects);

        _projectId = _projects.Create("Grid Storage 2026").Id;
        _resourceA = _resources.AddText(_projectId, "Storage economics", "Grid-scale storage LCOS is falling.");
        _resourceB = _resources.AddText(_projectId, "Demand outlook", "Grid storage demand grows through 2036.");
    }

    public void Dispose()
    {
        _store.ClearConnectionPool();
        _store.Dispose();
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

    private ArtifactVersion CurrentVersion(Artifact artifact)
    {
        using var db = _store.CreateDbContext();
        return db.ArtifactVersions.AsNoTracking().Single(v => v.Id == artifact.CurrentVersionId);
    }

    private IReadOnlyList<ChatResource> EnabledScope() =>
        _resources.ListEnabled(_projectId)
            .Select(r => new ChatResource(r.Id, r.Title, _resources.GetExtractedText(r.Id)))
            .ToList();

    // ----- Config-driven catalog -----

    // Scenario: The template catalog loads from config JSON
    [Fact]
    public void The_template_catalog_loads_from_config_JSON()
    {
        // When the template library is loaded
        var catalog = TemplateCatalogLoader.Default;

        // Then the templates come from the config file, not hard-coded values:
        // every template id is present in the raw config JSON (it is parsed from config, not C#).
        Assert.NotEmpty(catalog.Templates);
        var json = TemplateCatalogLoader.DefaultJson;
        foreach (var template in catalog.Templates)
            Assert.Contains($"\"{template.Id}\"", json, StringComparison.Ordinal);
    }

    // Scenario: A user-provided template is added without a rebuild
    [Fact]
    public void A_user_provided_template_is_added_without_a_rebuild()
    {
        // Given a Settings override that adds a template "House Brief"
        const string overrideJson = """
        {
          "templates": [
            {
              "id": "house-brief",
              "name": "House Brief",
              "description": "The firm's house brief format.",
              "targetType": "doc",
              "defaultModelTier": "Balanced",
              "sectionScaffold": [ "Summary", "Details" ],
              "generationPrompt": "Write a house brief on {scope} over {horizon} for {region}."
            }
          ]
        }
        """;

        // When the template library is loaded
        var result = TemplateCatalogLoader.LoadWithOverride(overrideJson);

        // Then "House Brief" appears in the gallery alongside the bundled templates
        Assert.False(result.HasErrors);
        Assert.Contains(result.Catalog.Templates, t => t.Name == "House Brief");
        // ...alongside the bundled ones (the 8 defaults are still present).
        Assert.Equal(TemplateCatalogLoader.Default.Templates.Count + 1, result.Catalog.Templates.Count);
        Assert.Contains(result.Catalog.Templates, t => t.Name == "Market Research Report");
    }

    // Scenario: A malformed template config surfaces a clear error, not a crash
    [Fact]
    public void A_malformed_template_config_surfaces_a_clear_error_not_a_crash()
    {
        // Given a template config missing a required "id" on one entry
        const string json = """
        {
          "templates": [
            {
              "name": "No Id Template",
              "description": "This entry is missing its id.",
              "targetType": "doc",
              "defaultModelTier": "Balanced",
              "sectionScaffold": [ "A" ],
              "generationPrompt": "Prompt {scope}."
            },
            {
              "id": "valid-one",
              "name": "Valid One",
              "description": "A valid entry.",
              "targetType": "doc",
              "defaultModelTier": "Balanced",
              "sectionScaffold": [ "A" ],
              "generationPrompt": "Prompt {scope}."
            }
          ]
        }
        """;

        // When the template library is loaded
        var result = TemplateCatalogLoader.Load(json);

        // Then loading reports a descriptive validation error identifying the bad entry
        Assert.True(result.HasErrors);
        var error = Assert.Single(result.Errors);
        Assert.Contains("id", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No Id Template", error, StringComparison.Ordinal);

        // And the valid entries still load
        var valid = Assert.Single(result.Catalog.Templates);
        Assert.Equal("valid-one", valid.Id);
    }

    // ----- Bundled templates (§3.4.1 table) -----

    // Scenario: All eight bundled templates are present with their target types
    [Fact]
    public void All_eight_bundled_templates_are_present_with_their_target_types()
    {
        // When the default catalog is loaded
        var catalog = TemplateCatalogLoader.Default;

        // Then it contains these templates and target artifact types:
        var expected = new (string Template, string TargetType)[]
        {
            ("Market Research Report", "doc"),
            ("Executive Summary / Brief", "doc"),
            ("Competitive Landscape", "table"),
            ("Market Forecast Model", "table"),
            ("SWOT / Porter's Five Forces", "doc"),
            ("Company / Vendor Profile", "doc"),
            ("Customer / Buyer Insights", "doc"),
            ("Trend / Technology Scan", "doc"),
        };

        Assert.Equal(8, catalog.Templates.Count);
        foreach (var (name, targetType) in expected)
        {
            var template = catalog.Resolve(name);
            Assert.NotNull(template);
            Assert.Equal(targetType, template!.TargetType);
        }
    }

    // ----- Template fields (§3.4.1) -----

    // Scenario: A template declares all required fields
    [Fact]
    public void A_template_declares_all_required_fields()
    {
        // Given the "Market Research Report" template
        var template = _catalog.Resolve("Market Research Report");
        Assert.NotNull(template);

        // Then it declares an id, display name, description, target artifact type, section scaffold,
        // a generation prompt, and a default model tier.
        Assert.False(string.IsNullOrWhiteSpace(template!.Id));
        Assert.False(string.IsNullOrWhiteSpace(template.Name));
        Assert.False(string.IsNullOrWhiteSpace(template.Description));
        Assert.False(string.IsNullOrWhiteSpace(template.TargetType));
        Assert.NotEmpty(template.SectionScaffold);
        Assert.False(string.IsNullOrWhiteSpace(template.GenerationPrompt));
        Assert.False(string.IsNullOrWhiteSpace(template.DefaultModelTier));
    }

    // Scenario: The Market Research Report scaffold has the specified sections
    [Fact]
    public void The_Market_Research_Report_scaffold_has_the_specified_sections()
    {
        // Given the "Market Research Report" template
        var template = _catalog.Resolve("Market Research Report");
        Assert.NotNull(template);

        // Then its section scaffold includes the specified sections.
        Assert.Contains("Executive summary", template!.SectionScaffold);
        Assert.Contains("Market sizing & 10-yr forecast", template.SectionScaffold);
        Assert.Contains("Competitive landscape", template.SectionScaffold);
        Assert.Contains("Regional analysis", template.SectionScaffold);
        Assert.Contains("Methodology & sources", template.SectionScaffold);
    }

    // Scenario Outline: A template recommends a default model tier
    [Theory]
    [InlineData("Market Research Report", "Deep")]
    [InlineData("Executive Summary / Brief", "Balanced")]
    public void A_template_recommends_a_default_model_tier(string templateName, string tier)
    {
        // Given the "<template>" template
        var template = _catalog.Resolve(templateName);
        Assert.NotNull(template);

        // Then its default model tier is "<tier>"
        Assert.Equal(tier, template!.DefaultModelTier);
    }

    // ----- Prompt placeholders — scope / horizon / region -----

    // Scenario: Placeholders are substituted into the generation prompt
    [Fact]
    public void Placeholders_are_substituted_into_the_generation_prompt()
    {
        // Given the "Market Research Report" template whose prompt contains {scope}, {horizon}, {region}
        var template = _catalog.Resolve("Market Research Report");
        Assert.NotNull(template);
        Assert.Contains("{scope}", template!.GenerationPrompt);
        Assert.Contains("{horizon}", template.GenerationPrompt);
        Assert.Contains("{region}", template.GenerationPrompt);

        // When I supply scope, horizon, and region
        var parameters = new TemplatePromptParameters(
            Scope: "Grid-scale battery storage", Horizon: "2026–2036", Region: "North America");
        var assembled = TemplatePromptAssembler.Assemble(template, parameters, EnabledScope());

        // Then the assembled prompt contains the supplied values
        Assert.Contains("Grid-scale battery storage", assembled);
        Assert.Contains("2026–2036", assembled);
        Assert.Contains("North America", assembled);

        // And no unresolved "{scope}", "{horizon}", or "{region}" placeholder remains
        Assert.DoesNotContain("{scope}", assembled);
        Assert.DoesNotContain("{horizon}", assembled);
        Assert.DoesNotContain("{region}", assembled);
    }

    // Scenario: An unfilled optional placeholder falls back to a sensible default
    [Fact]
    public void An_unfilled_optional_placeholder_falls_back_to_a_sensible_default()
    {
        // Given a template with a {region} placeholder
        var template = _catalog.Resolve("Market Research Report");
        Assert.NotNull(template);
        Assert.Contains("{region}", template!.GenerationPrompt);

        // When I leave region blank
        var parameters = new TemplatePromptParameters(Scope: "Storage", Horizon: "2026–2036", Region: null);
        var assembled = TemplatePromptAssembler.Assemble(template, parameters, EnabledScope());

        // Then the assembled prompt uses "Global" for region
        Assert.Contains("Global", assembled);
        Assert.DoesNotContain("{region}", assembled);
    }

    // ----- Grounding-first prompting (§3.4.1) -----

    // Scenario: The assembled prompt instructs the model to cite in-scope resources
    [Fact]
    public void The_assembled_prompt_instructs_the_model_to_cite_in_scope_resources()
    {
        // Given any bundled template
        var template = _catalog.Templates[0];

        // When I assemble its generation prompt with 2 in-scope resources
        var scope = EnabledScope();
        Assert.Equal(2, scope.Count);
        var assembled = TemplatePromptAssembler.Assemble(template, new TemplatePromptParameters(), scope);

        // Then the prompt instructs the model to cite which in-scope resource supports each claim
        Assert.Contains("cite which in-scope resource supports each claim", assembled, StringComparison.OrdinalIgnoreCase);
        // ...naming the in-scope resources.
        Assert.Contains(_resourceA.Id, assembled, StringComparison.Ordinal);
        Assert.Contains(_resourceB.Id, assembled, StringComparison.Ordinal);
    }

    // Scenario: The assembled prompt instructs the model to flag unsupported claims
    [Fact]
    public void The_assembled_prompt_instructs_the_model_to_flag_unsupported_claims()
    {
        // Given any bundled template
        var template = _catalog.Templates[0];

        // When I assemble its generation prompt
        var assembled = TemplatePromptAssembler.Assemble(template, new TemplatePromptParameters(), EnabledScope());

        // Then the prompt instructs the model to flag assertions not supported by the in-scope resources
        Assert.Contains("flag", assembled, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not supported by the in-scope resources", assembled, StringComparison.OrdinalIgnoreCase);
    }

    // Scenario: Only enabled resources are passed as in-scope for grounding
    [Fact]
    public async Task Only_enabled_resources_are_passed_as_in_scope_for_grounding()
    {
        // Given a project with 3 resources, one disabled
        var resourceC = _resources.AddText(_projectId, "Draft notes", "Rough working notes.");
        _resources.SetEnabled(resourceC.Id, false);

        // When I generate from a template
        var artifact = await _templates.GenerateFromTemplate(
            _projectId, "market-research-report", new TemplatePromptParameters(Scope: "Storage"));

        // Then the version's resource_scope_json contains only the 2 enabled resource ids
        var version = CurrentVersion(artifact);
        Assert.NotNull(version.ResourceScopeJson);
        Assert.Contains(_resourceA.Id, version.ResourceScopeJson!);
        Assert.Contains(_resourceB.Id, version.ResourceScopeJson!);
        Assert.DoesNotContain(resourceC.Id, version.ResourceScopeJson!);
    }

    // ----- Generate from a template (produces an artifact) -----

    // Scenario: Generating from a template creates an artifact of the template's target type
    [Fact]
    public async Task Generating_from_a_template_creates_an_artifact_of_the_templates_target_type()
    {
        // Given the "Competitive Landscape" template (target type "table")
        var template = _catalog.Resolve("Competitive Landscape");
        Assert.NotNull(template);
        Assert.Equal("table", template!.TargetType);
        var expectedPrompt = TemplatePromptAssembler.Assemble(
            template, new TemplatePromptParameters(Scope: "EV charging networks"), EnabledScope());
        var expectedModel = _models.Resolve(template.DefaultModelTier)!.Id;

        // When I generate from it with scope "EV charging networks"
        var artifact = await _templates.GenerateFromTemplate(
            _projectId, "Competitive Landscape", new TemplatePromptParameters(Scope: "EV charging networks"));

        // Then an artifact of type "table" is created
        Assert.Equal("table", _artifacts.Get(artifact.Id)!.Type);

        // And its first version records the template's id, the assembled prompt, the model, and the in-scope resources
        var version = CurrentVersion(artifact);
        Assert.Equal(1, version.VersionNo);
        Assert.Equal(expectedPrompt, version.Prompt);
        Assert.Contains(template.Id, version.Prompt!, StringComparison.Ordinal);
        Assert.Equal(expectedModel, version.Model);
        Assert.NotNull(version.ResourceScopeJson);
        Assert.Contains(_resourceA.Id, version.ResourceScopeJson!);
        Assert.Contains(_resourceB.Id, version.ResourceScopeJson!);
    }

    // Scenario: The generated artifact follows the template's section scaffold
    [Fact]
    public async Task The_generated_artifact_follows_the_templates_section_scaffold()
    {
        // Given the "Market Research Report" template
        var template = _catalog.Resolve("Market Research Report");
        Assert.NotNull(template);

        // And a FakeChatService scripted to echo the scaffold headings
        _chat.WithCompletionText(string.Join("\n", template!.SectionScaffold));

        // When I generate from it
        var artifact = await _templates.GenerateFromTemplate(
            _projectId, "Market Research Report", new TemplatePromptParameters(Scope: "Storage"));

        // Then the artifact content contains the scaffold's section headings in order
        var content = CurrentVersion(artifact).Content;
        var lastIndex = -1;
        foreach (var heading in template.SectionScaffold)
        {
            var index = content.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Heading '{heading}' missing from generated content.");
            Assert.True(index > lastIndex, $"Heading '{heading}' out of order in generated content.");
            lastIndex = index;
        }
    }

    // ----- Flagship — Market Research Report (§9.1(5)) -----

    // Scenario: Generate a Market Research Report artifact from the flagship template
    [Fact]
    public async Task Generate_a_Market_Research_Report_artifact_from_the_flagship_template()
    {
        // Given the "Market Research Report" template (FakeChatService reports usage)
        _chat.WithCompletionText("A grounded market research report.").WithUsage(3000, 2200);
        var template = _catalog.Resolve("Market Research Report");
        Assert.NotNull(template);
        var expectedModel = _models.Resolve(template!.DefaultModelTier)!.Id;

        // When I generate from it with scope, horizon, region
        var artifact = await _templates.GenerateFromTemplate(
            _projectId,
            "Market Research Report",
            new TemplatePromptParameters(Scope: "Grid-scale storage", Horizon: "2026–2036", Region: "North America"));

        // Then a "doc" artifact titled from the template is created
        var loaded = _artifacts.Get(artifact.Id);
        Assert.NotNull(loaded);
        Assert.Equal("doc", loaded!.Type);
        Assert.Equal(template.Name, loaded.Title);

        var version = CurrentVersion(loaded);

        // And it is grounded in the project's enabled resources
        Assert.NotNull(version.ResourceScopeJson);
        Assert.Contains(_resourceA.Id, version.ResourceScopeJson!);
        Assert.Contains(_resourceB.Id, version.ResourceScopeJson!);

        // And its version records model, prompt, in-scope resources, and usage
        Assert.Equal(expectedModel, version.Model);
        Assert.False(string.IsNullOrWhiteSpace(version.Prompt));
        Assert.Contains("Grid-scale storage", version.Prompt!);
        Assert.Equal("claude", version.CreatedBy);
        Assert.Equal(3000, version.TokensIn);
        Assert.Equal(2200, version.TokensOut);
    }

    // ----- New project from a template -----

    // Scenario: Creating a project from a template seeds a first artifact from that template
    [Fact]
    public async Task Creating_a_project_from_a_template_seeds_a_first_artifact_from_that_template()
    {
        // Given the "Market Research Report" template
        // When I create a project "Storage Study" from it
        var project = await _templates.CreateProjectFromTemplate("Market Research Report", "Storage Study");

        // Then a project "Storage Study" exists
        Assert.Equal("Storage Study", _projects.Get(project.Id)!.Name);

        // And it contains a Market Research Report artifact generated from the template
        var artifacts = _artifacts.List(project.Id);
        var seeded = Assert.Single(artifacts);
        Assert.Equal("Market Research Report", seeded.Title);
        Assert.Equal("doc", seeded.Type);
        var version = CurrentVersion(seeded);
        Assert.Equal("claude", version.CreatedBy);
        Assert.Contains("market-research-report", version.Prompt!, StringComparison.Ordinal);
    }
}
