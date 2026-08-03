using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Models;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.Core.Templates;

/// <summary>
/// Default <see cref="ITemplateService"/> (SPEC §3.4.1): assembles a grounding-first request from a
/// deliverable template and routes it through the artifact-creation <see cref="IArtifactService.Generate"/>
/// seam, and composes projects-crud creation with generation for "new project from template".
/// </summary>
public sealed class TemplateService : ITemplateService
{
    private readonly ITemplateCatalog _catalog;
    private readonly IArtifactService _artifacts;
    private readonly IResourceService _resources;
    private readonly IModelCatalog _models;
    private readonly IProjectService _projects;

    /// <summary>Creates the template service over its collaborators.</summary>
    /// <param name="catalog">The deliverable-template catalog.</param>
    /// <param name="artifacts">The artifact-creation generation seam.</param>
    /// <param name="resources">The resource service (enabled-only scope).</param>
    /// <param name="models">The model catalog used to resolve a tier to a concrete model.</param>
    /// <param name="projects">The project service used by "new project from template".</param>
    /// <exception cref="ArgumentNullException">A collaborator is null.</exception>
    public TemplateService(
        ITemplateCatalog catalog,
        IArtifactService artifacts,
        IResourceService resources,
        IModelCatalog models,
        IProjectService projects)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
    }

    /// <inheritdoc />
    public ITemplateCatalog Catalog => _catalog;

    /// <inheritdoc />
    public Task<Artifact> GenerateFromTemplate(
        string projectId,
        string templateId,
        TemplatePromptParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var template = _catalog.Resolve(templateId) ?? throw new TemplateNotFoundException(templateId);

        // In-scope = enabled resources only (SPEC §3.4.1 grounding).
        var inScope = _resources.ListEnabled(projectId)
            .Select(r => new ChatResource(r.Id, r.Title, _resources.GetExtractedText(r.Id)))
            .ToList();

        var model = _models.Resolve(template.DefaultModelTier)?.Id ?? _models.DefaultModelId;
        var prompt = TemplatePromptAssembler.Assemble(template, parameters, inScope);

        var request = new GenerateArtifactRequest
        {
            Type = template.TargetType,
            Title = template.Name,
            Prompt = prompt,
            Model = model,
            Resources = inScope,
        };

        return _artifacts.Generate(projectId, request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Project> CreateProjectFromTemplate(
        string templateId,
        string projectName,
        TemplatePromptParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        // Resolve up-front so a bad template id never creates a stray project.
        _ = _catalog.Resolve(templateId) ?? throw new TemplateNotFoundException(templateId);

        var project = _projects.Create(projectName);
        await GenerateFromTemplate(
            project.Id, templateId, parameters ?? new TemplatePromptParameters(), cancellationToken)
            .ConfigureAwait(false);
        return project;
    }
}
