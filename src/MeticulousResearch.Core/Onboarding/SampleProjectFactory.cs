using MeticulousResearch.Core.Ai;
using MeticulousResearch.Core.Artifacts;
using MeticulousResearch.Core.Data.Entities;
using MeticulousResearch.Core.Projects;
using MeticulousResearch.Core.Resources;

namespace MeticulousResearch.Core.Onboarding;

/// <summary>
/// Default <see cref="ISampleProjectFactory"/> (SPEC §3.8(4)). Composes the existing project,
/// resource, and artifact domain services to seed a populated sample project entirely from bundled
/// content: it never calls the model (<see cref="IArtifactService.CreateFromContent"/> with static
/// text, not <c>Generate</c>) so it works offline and without an API key.
/// </summary>
public sealed class SampleProjectFactory : ISampleProjectFactory
{
    private readonly IProjectService _projects;
    private readonly IResourceService _resources;
    private readonly IArtifactService _artifacts;

    /// <summary>Creates the factory over the project, resource, and artifact domain services.</summary>
    public SampleProjectFactory(
        IProjectService projects,
        IResourceService resources,
        IArtifactService artifacts)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
    }

    /// <inheritdoc />
    public Project CreateSampleProject()
    {
        var project = _projects.Create(
            SampleContent.ProjectName,
            SampleContent.ProjectDescription,
            SampleContent.ProjectInstructions);

        _resources.AddText(project.Id, SampleContent.ResourceOneTitle, SampleContent.ResourceOneText);
        _resources.AddText(project.Id, SampleContent.ResourceTwoTitle, SampleContent.ResourceTwoText);

        _artifacts.CreateFromContent(
            project.Id,
            ArtifactTypes.Doc,
            SampleContent.ArtifactTitle,
            SampleContent.MarketResearchReport,
            contentFormat: null,
            provenance: ArtifactProvenance.User());

        return project;
    }
}
