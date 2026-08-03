namespace MeticulousResearch.Core.Tests;

/// <summary>
/// A disposable temporary directory for @integration tests that touch the filesystem/SQLite.
/// Created under the OS temp path and deleted on dispose so no test touches real app data
/// (TESTING-STRATEGY §4).
/// </summary>
public sealed class TempDataDirectory : IDisposable
{
    public TempDataDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>The absolute path to the temporary directory.</summary>
    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; leftover temp files are harmless.
        }
    }
}
