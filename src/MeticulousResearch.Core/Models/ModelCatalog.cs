using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeticulousResearch.Core.Models;

/// <summary>
/// The in-memory <see cref="IModelCatalog"/> parsed from the config-driven catalog JSON (SPEC §6.3).
/// Immutable after construction; use <see cref="ModelCatalogLoader"/> to build one from the shipped
/// default or a user-supplied JSON/file with fallback semantics.
/// </summary>
public sealed class ModelCatalog : IModelCatalog
{
    private readonly IReadOnlyList<ModelInfo> _tiers;
    private readonly IReadOnlyList<ModelInfo> _additional;
    private readonly Dictionary<string, ModelInfo> _byId;

    /// <summary>Creates a catalog from its tiers, additional models, and default model id.</summary>
    /// <param name="tiers">The friendly tiers, in display order.</param>
    /// <param name="additional">The additional (non-tier) models.</param>
    /// <param name="defaultModelId">The default project model id.</param>
    public ModelCatalog(IReadOnlyList<ModelInfo> tiers, IReadOnlyList<ModelInfo> additional, string defaultModelId)
    {
        _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
        _additional = additional ?? throw new ArgumentNullException(nameof(additional));
        DefaultModelId = defaultModelId ?? throw new ArgumentNullException(nameof(defaultModelId));

        _byId = new Dictionary<string, ModelInfo>(StringComparer.Ordinal);
        foreach (var model in _tiers.Concat(_additional))
            _byId[model.Id] = model;
    }

    /// <inheritdoc />
    public IReadOnlyList<ModelInfo> Tiers => _tiers;

    /// <inheritdoc />
    public IReadOnlyList<ModelInfo> AdditionalModels => _additional;

    /// <inheritdoc />
    public string DefaultModelId { get; }

    /// <inheritdoc />
    public ModelInfo? Resolve(string tierOrId)
    {
        if (string.IsNullOrWhiteSpace(tierOrId))
            return null;

        var tier = _tiers.FirstOrDefault(t =>
            string.Equals(t.Tier, tierOrId, StringComparison.OrdinalIgnoreCase));
        if (tier is not null)
            return tier;

        return TryGet(tierOrId);
    }

    /// <inheritdoc />
    public ModelInfo? TryGet(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        return _byId.TryGetValue(id, out var model) ? model : null;
    }

    /// <inheritdoc />
    public bool IsVisionCapable(string id) => TryGet(id)?.Vision ?? false;

    /// <inheritdoc />
    public ModelPrice? GetPrice(string id)
    {
        var model = TryGet(id);
        return model is null ? null : new ModelPrice(model.PriceInputMTok, model.PriceOutputMTok);
    }
}

/// <summary>The outcome of loading a catalog: the resulting catalog and an optional fallback warning.</summary>
/// <param name="Catalog">The loaded catalog, or the shipped default when a fallback occurred.</param>
/// <param name="Warning">A human-readable warning (no stack trace) when the input was malformed, else <c>null</c>.</param>
public sealed record ModelCatalogLoadResult(IModelCatalog Catalog, string? Warning)
{
    /// <summary>Whether the shipped default was substituted because the supplied input was invalid.</summary>
    public bool UsedFallback => Warning is not null;
}

/// <summary>
/// Loads an <see cref="IModelCatalog"/> from the shipped default JSON or a user override (SPEC §6.3).
/// Loading is pure/deterministic: malformed JSON or a schema-invalid catalog falls back to the
/// shipped default and surfaces a human-readable warning rather than throwing.
/// </summary>
public static class ModelCatalogLoader
{
    /// <summary>The embedded shipped-default catalog resource file name.</summary>
    private const string DefaultResourceSuffix = "Models.default-model-catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<string> DefaultJsonText = new(ReadEmbeddedDefault);
    private static readonly Lazy<IModelCatalog> DefaultCatalog = new(() => ParseOrThrow(DefaultJsonText.Value));

    /// <summary>The shipped default catalog (SPEC §6.3): the four tiers plus the additional models.</summary>
    public static IModelCatalog Default => DefaultCatalog.Value;

    /// <summary>
    /// Parses a catalog from a JSON string. When <paramref name="json"/> is null/blank the shipped
    /// default is returned without a warning; when it is malformed or schema-invalid the shipped
    /// default is returned with a human-readable warning.
    /// </summary>
    /// <param name="json">The catalog JSON, or <c>null</c>/blank to use the shipped default.</param>
    public static ModelCatalogLoadResult Load(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ModelCatalogLoadResult(Default, null);

        try
        {
            return new ModelCatalogLoadResult(ParseOrThrow(json), null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidModelCatalogException)
        {
            return new ModelCatalogLoadResult(
                Default,
                $"The model catalog could not be read ({ex.Message}). Using the built-in default catalog.");
        }
    }

    /// <summary>
    /// Loads a catalog from a file path. A null/blank path or a missing file uses the shipped default
    /// without a warning; a malformed or schema-invalid file falls back with a warning.
    /// </summary>
    /// <param name="path">The catalog file path, or <c>null</c>/blank to use the shipped default.</param>
    public static ModelCatalogLoadResult LoadFromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new ModelCatalogLoadResult(Default, null);

        try
        {
            return Load(File.ReadAllText(path));
        }
        catch (IOException ex)
        {
            return new ModelCatalogLoadResult(
                Default,
                $"The model catalog file could not be read ({ex.Message}). Using the built-in default catalog.");
        }
    }

    private static IModelCatalog ParseOrThrow(string json)
    {
        var dto = JsonSerializer.Deserialize<CatalogDto>(json, JsonOptions)
            ?? throw new InvalidModelCatalogException("the file was empty");

        if (string.IsNullOrWhiteSpace(dto.DefaultModel))
            throw new InvalidModelCatalogException("no default model was specified");

        var tiers = (dto.Tiers ?? new List<ModelDto>()).Select(ToModel).ToList();
        var additional = (dto.Additional ?? new List<ModelDto>()).Select(ToModel).ToList();

        if (tiers.Count == 0)
            throw new InvalidModelCatalogException("no tiers were specified");

        var known = tiers.Concat(additional).Any(m => string.Equals(m.Id, dto.DefaultModel, StringComparison.Ordinal));
        if (!known)
            throw new InvalidModelCatalogException($"the default model '{dto.DefaultModel}' is not in the catalog");

        return new ModelCatalog(tiers, additional, dto.DefaultModel!);
    }

    private static ModelInfo ToModel(ModelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
            throw new InvalidModelCatalogException("a model entry is missing its id");

        return new ModelInfo
        {
            Tier = string.IsNullOrWhiteSpace(dto.Tier) ? null : dto.Tier,
            Name = dto.Name ?? dto.Id!,
            Id = dto.Id!,
            ContextTokens = dto.ContextTokens,
            MaxOutputTokens = dto.MaxOutputTokens,
            PriceInputMTok = dto.PriceInputMTok,
            PriceOutputMTok = dto.PriceOutputMTok,
            Vision = dto.Vision,
        };
    }

    private static string ReadEmbeddedDefault()
    {
        var assembly = typeof(ModelCatalogLoader).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(DefaultResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The shipped default catalog resource '{DefaultResourceSuffix}' was not found in the assembly.");

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The catalog resource '{name}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class CatalogDto
    {
        [JsonPropertyName("defaultModel")] public string? DefaultModel { get; set; }
        [JsonPropertyName("tiers")] public List<ModelDto>? Tiers { get; set; }
        [JsonPropertyName("additional")] public List<ModelDto>? Additional { get; set; }
    }

    private sealed class ModelDto
    {
        [JsonPropertyName("tier")] public string? Tier { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("contextTokens")] public int ContextTokens { get; set; }
        [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; set; }
        [JsonPropertyName("priceInputMTok")] public double PriceInputMTok { get; set; }
        [JsonPropertyName("priceOutputMTok")] public double PriceOutputMTok { get; set; }
        [JsonPropertyName("vision")] public bool Vision { get; set; }
    }
}

/// <summary>Raised internally when a parsed catalog is structurally valid JSON but semantically invalid.</summary>
public sealed class InvalidModelCatalogException : Exception
{
    /// <summary>Creates the exception with a human-readable reason (no stack trace surfaced to users).</summary>
    public InvalidModelCatalogException(string message) : base(message)
    {
    }
}
