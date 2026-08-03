using System.Text;
using MeticulousResearch.Core.Ai;

namespace MeticulousResearch.Core.Templates;

/// <summary>
/// The scope/horizon/region parameters supplied when assembling a template's generation prompt.
/// Any unfilled optional falls back to a sensible default (e.g. region → <c>Global</c>) so no
/// unresolved placeholder ever remains in the assembled prompt (SPEC §3.4.1).
/// </summary>
/// <param name="Scope">The subject/market in scope (optional).</param>
/// <param name="Horizon">The time horizon (optional).</param>
/// <param name="Region">The geographic region (optional; defaults to <c>Global</c>).</param>
public sealed record TemplatePromptParameters(string? Scope = null, string? Horizon = null, string? Region = null);

/// <summary>
/// Assembles a template's generation prompt (SPEC §3.4.1): substitutes the <c>{scope}</c>,
/// <c>{horizon}</c>, and <c>{region}</c> placeholders (applying sensible defaults for blanks) and
/// prepends the shared <b>grounding-first</b> preamble that instructs the model to cite which
/// in-scope resource supports each claim and to flag assertions not supported by those resources.
/// The preamble lives in one place so "cite sources / flag unsupported claims" is tuned once, not
/// duplicated per template.
/// </summary>
public static class TemplatePromptAssembler
{
    /// <summary>Default region when none is supplied.</summary>
    public const string DefaultRegion = "Global";

    /// <summary>Default scope when none is supplied.</summary>
    public const string DefaultScope = "the market in scope";

    /// <summary>Default horizon when none is supplied.</summary>
    public const string DefaultHorizon = "the next 10 years";

    /// <summary>
    /// Assembles the full generation prompt for <paramref name="template"/>: the grounding-first
    /// preamble (listing the in-scope resources), a template-id marker so the produced version can be
    /// traced back to its template, and the placeholder-substituted generation prompt.
    /// </summary>
    /// <param name="template">The template whose prompt to assemble.</param>
    /// <param name="parameters">The scope/horizon/region parameters (blanks fall back to defaults).</param>
    /// <param name="inScopeResources">The enabled resources in scope for grounding (may be empty).</param>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> or <paramref name="parameters"/> is null.</exception>
    public static string Assemble(
        DeliverableTemplate template,
        TemplatePromptParameters parameters,
        IReadOnlyList<ChatResource> inScopeResources)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);
        inScopeResources ??= Array.Empty<ChatResource>();

        var scope = Fallback(parameters.Scope, DefaultScope);
        var horizon = Fallback(parameters.Horizon, DefaultHorizon);
        var region = Fallback(parameters.Region, DefaultRegion);

        var body = template.GenerationPrompt
            .Replace("{scope}", scope, StringComparison.Ordinal)
            .Replace("{horizon}", horizon, StringComparison.Ordinal)
            .Replace("{region}", region, StringComparison.Ordinal);

        var sb = new StringBuilder();
        sb.Append("Template: ").Append(template.Id).Append(" (").Append(template.Name).Append(')').Append('\n');
        sb.Append(GroundingPreamble(inScopeResources)).Append('\n').Append('\n');
        sb.Append(body).Append('\n').Append('\n');
        sb.Append("Follow this section scaffold, in order:").Append('\n');
        foreach (var heading in template.SectionScaffold)
            sb.Append("- ").Append(heading).Append('\n');

        return sb.ToString();
    }

    /// <summary>
    /// The shared grounding-first preamble: instruct the model to cite which in-scope resource
    /// supports each claim and to flag any assertion not supported by the in-scope resources.
    /// </summary>
    /// <param name="inScopeResources">The enabled resources in scope for grounding.</param>
    public static string GroundingPreamble(IReadOnlyList<ChatResource> inScopeResources)
    {
        var sb = new StringBuilder();
        sb.Append("Ground every statement in the in-scope resources. ");
        sb.Append("Cite which in-scope resource supports each claim, ");
        sb.Append("and flag any assertion not supported by the in-scope resources.").Append('\n');

        if (inScopeResources.Count == 0)
        {
            sb.Append("In-scope resources: (none enabled).");
        }
        else
        {
            sb.Append("In-scope resources:").Append('\n');
            foreach (var resource in inScopeResources)
                sb.Append("- ").Append(resource.Title).Append(" [").Append(resource.Id).Append(']').Append('\n');
        }

        return sb.ToString();
    }

    private static string Fallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
