namespace MeticulousResearch.Core.Data.Entities;

/// <summary>
/// An app-level key/value setting. Secrets do NOT live here — they go to the credential vault
/// (settings-secure-key). Maps to the <c>Setting</c> table (SPEC §5).
/// </summary>
public sealed class Setting
{
    /// <summary>Setting key (primary key).</summary>
    public string Key { get; set; } = "";

    /// <summary>Setting value (opaque string; callers own serialization).</summary>
    public string? Value { get; set; }
}
