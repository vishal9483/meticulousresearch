using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Templates;

/// <summary>
/// Drives artifact generation from a deliverable template (SPEC §3.4.1): resolves the template,
/// filters the project's enabled resources into scope, resolves the template's default model tier to
/// a concrete model, assembles the grounding-first prompt, and generates the artifact through the
/// artifact-creation <c>Generate</c> seam. Also composes projects-crud creation with generation to
/// start a new project from a template (SPEC §9.1(2)). Owned by <c>deliverable-templates</c>.
/// </summary>
public interface ITemplateService
{
    /// <summary>The template catalog surfaced in the gallery flows.</summary>
    ITemplateCatalog Catalog { get; }

    /// <summary>
    /// Generates an artifact of the template's target type from <paramref name="templateId"/>,
    /// grounded in the project's <em>enabled</em> resources. The resulting version records the
    /// assembled prompt (which carries the template id), the resolved model, the in-scope resource
    /// ids, and the usage.
    /// </summary>
    /// <param name="projectId">The owning project.</param>
    /// <param name="templateId">The template id or display name.</param>
    /// <param name="parameters">The scope/horizon/region parameters (blanks fall back to defaults).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TemplateNotFoundException">No template matches <paramref name="templateId"/>.</exception>
    Task<Artifact> GenerateFromTemplate(
        string projectId,
        string templateId,
        TemplatePromptParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new project named <paramref name="projectName"/> and seeds a first artifact from
    /// <paramref name="templateId"/> (SPEC §9.1(2)).
    /// </summary>
    /// <param name="templateId">The template id or display name.</param>
    /// <param name="projectName">The new project's name.</param>
    /// <param name="parameters">The scope/horizon/region parameters (blanks fall back to defaults).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TemplateNotFoundException">No template matches <paramref name="templateId"/>.</exception>
    Task<Project> CreateProjectFromTemplate(
        string templateId,
        string projectName,
        TemplatePromptParameters? parameters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Raised when a template id or name does not resolve to a template in the catalog.</summary>
public sealed class TemplateNotFoundException : Exception
{
    /// <summary>Creates the exception naming the unresolved template id/name.</summary>
    public TemplateNotFoundException(string idOrName)
        : base($"No deliverable template matches '{idOrName}'.")
    {
    }
}
