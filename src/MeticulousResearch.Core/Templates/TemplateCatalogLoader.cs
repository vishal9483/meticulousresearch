using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeticulousResearch.Core.Templates;

/// <summary>The outcome of loading a template catalog: the resulting catalog plus any validation errors.</summary>
/// <param name="Catalog">The catalog of the valid entries that loaded.</param>
/// <param name="Errors">
/// One descriptive message per malformed entry (identifying the bad entry), empty when all entries
/// were valid. The valid entries still load even when some entries are rejected (SPEC §3.4.1).
/// </param>
public sealed record TemplateCatalogLoadResult(ITemplateCatalog Catalog, IReadOnlyList<string> Errors)
{
    /// <summary>Whether at least one entry failed validation.</summary>
    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// Loads an <see cref="ITemplateCatalog"/> from the shipped default JSON merged with a Settings
/// override (SPEC §3.4.1, mirroring the model-catalog philosophy §6.3). The JSON is the source of
/// truth: templates are parsed from config, never hard-coded in C#. Malformed entries (e.g. a
/// missing required <c>id</c>) surface a descriptive validation error identifying the bad entry
/// without crashing, and the valid entries still load.
/// </summary>
public static class TemplateCatalogLoader
{
    /// <summary>The embedded shipped-default catalog resource file name suffix.</summary>
    private const string DefaultResourceSuffix = "Templates.default-template-catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<string> DefaultJsonText = new(ReadEmbeddedDefault);
    private static readonly Lazy<ITemplateCatalog> DefaultCatalog = new(() => LoadOrThrowDefault());

    /// <summary>The raw shipped-default catalog JSON text (the config file, not hard-coded values).</summary>
    public static string DefaultJson => DefaultJsonText.Value;

    /// <summary>The shipped default catalog: the eight bundled templates (SPEC §3.4.1 table).</summary>
    public static ITemplateCatalog Default => DefaultCatalog.Value;

    /// <summary>
    /// Parses a full catalog from a JSON string. Malformed entries are skipped with a descriptive
    /// error; the valid entries still load. A null/blank input yields an empty catalog.
    /// </summary>
    /// <param name="json">The catalog JSON, or null/blank for an empty catalog.</param>
    public static TemplateCatalogLoadResult Load(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new TemplateCatalogLoadResult(new TemplateCatalog(Array.Empty<DeliverableTemplate>()), Array.Empty<string>());

        var (templates, errors) = Parse(json);
        return new TemplateCatalogLoadResult(new TemplateCatalog(templates), errors);
    }

    /// <summary>
    /// Loads the shipped default catalog merged with a Settings override. Override entries whose
    /// <c>id</c> matches a bundled template replace it; new ids are appended after the bundled set.
    /// A null/blank override yields the bundled defaults unchanged.
    /// </summary>
    /// <param name="overrideJson">The Settings override JSON, or null/blank for the defaults only.</param>
    public static TemplateCatalogLoadResult LoadWithOverride(string? overrideJson)
    {
        var merged = new List<DeliverableTemplate>(Default.Templates);
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(overrideJson))
        {
            var (extras, extraErrors) = Parse(overrideJson);
            errors.AddRange(extraErrors);
            foreach (var extra in extras)
            {
                var existing = merged.FindIndex(t => string.Equals(t.Id, extra.Id, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                    merged[existing] = extra;
                else
                    merged.Add(extra);
            }
        }

        return new TemplateCatalogLoadResult(new TemplateCatalog(merged), errors);
    }

    /// <summary>
    /// Loads the shipped default merged with an override file. A null/blank or missing path yields
    /// the bundled defaults unchanged.
    /// </summary>
    /// <param name="path">The override file path, or null/blank to use the defaults only.</param>
    public static TemplateCatalogLoadResult LoadFromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return LoadWithOverride(null);

        try
        {
            return LoadWithOverride(File.ReadAllText(path));
        }
        catch (IOException ex)
        {
            return new TemplateCatalogLoadResult(
                Default,
                new[] { $"The template catalog file could not be read ({ex.Message}). Using the built-in default catalog." });
        }
    }

    private static (List<DeliverableTemplate> Templates, List<string> Errors) Parse(string json)
    {
        var templates = new List<DeliverableTemplate>();
        var errors = new List<string>();

        CatalogDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CatalogDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            errors.Add($"The template catalog JSON could not be parsed ({ex.Message}).");
            return (templates, errors);
        }

        var entries = dto?.Templates ?? new List<TemplateDto>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var missing = MissingRequiredField(entry);
            if (missing is not null)
            {
                var label = string.IsNullOrWhiteSpace(entry.Id)
                    ? (string.IsNullOrWhiteSpace(entry.Name) ? $"entry #{i + 1}" : $"entry '{entry.Name}'")
                    : $"entry '{entry.Id}'";
                errors.Add($"Template {label} is missing a required '{missing}' field.");
                continue;
            }

            templates.Add(new DeliverableTemplate
            {
                Id = entry.Id!,
                Name = entry.Name!,
                Description = entry.Description ?? "",
                TargetType = entry.TargetType!,
                SectionScaffold = entry.SectionScaffold ?? new List<string>(),
                GenerationPrompt = entry.GenerationPrompt!,
                DefaultModelTier = entry.DefaultModelTier!,
            });
        }

        return (templates, errors);
    }

    private static string? MissingRequiredField(TemplateDto entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id)) return "id";
        if (string.IsNullOrWhiteSpace(entry.Name)) return "name";
        if (string.IsNullOrWhiteSpace(entry.TargetType)) return "targetType";
        if (string.IsNullOrWhiteSpace(entry.GenerationPrompt)) return "generationPrompt";
        if (string.IsNullOrWhiteSpace(entry.DefaultModelTier)) return "defaultModelTier";
        if (entry.SectionScaffold is null || entry.SectionScaffold.Count == 0) return "sectionScaffold";
        return null;
    }

    private static ITemplateCatalog LoadOrThrowDefault()
    {
        var (templates, errors) = Parse(DefaultJsonText.Value);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"The shipped default template catalog is invalid: {string.Join("; ", errors)}");
        return new TemplateCatalog(templates);
    }

    private static string ReadEmbeddedDefault()
    {
        var assembly = typeof(TemplateCatalogLoader).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(DefaultResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The shipped default template catalog resource '{DefaultResourceSuffix}' was not found in the assembly.");

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The template catalog resource '{name}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class CatalogDto
    {
        [JsonPropertyName("templates")] public List<TemplateDto>? Templates { get; set; }
    }

    private sealed class TemplateDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("targetType")] public string? TargetType { get; set; }
        [JsonPropertyName("sectionScaffold")] public List<string>? SectionScaffold { get; set; }
        [JsonPropertyName("generationPrompt")] public string? GenerationPrompt { get; set; }
        [JsonPropertyName("defaultModelTier")] public string? DefaultModelTier { get; set; }
    }
}
