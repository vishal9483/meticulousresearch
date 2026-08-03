namespace MeticulousResearch.Core.Settings;

/// <summary>
/// Validates that a candidate data directory is writable before it is persisted
/// (settings-secure-key/phase.md — data-directory change is validated before saving).
/// </summary>
public interface IDataDirectoryValidator
{
    /// <summary>Returns true when <paramref name="path"/> exists (or can be created) and is writable.</summary>
    bool IsWritable(string path);
}
