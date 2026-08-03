namespace MeticulousResearch.Core.Settings;

/// <summary>
/// Filesystem-backed <see cref="IDataDirectoryValidator"/>: probes writability by creating and
/// deleting a temporary file in the target directory (settings-secure-key/phase.md).
/// </summary>
public sealed class DataDirectoryValidator : IDataDirectoryValidator
{
    /// <inheritdoc />
    public bool IsWritable(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".write-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
