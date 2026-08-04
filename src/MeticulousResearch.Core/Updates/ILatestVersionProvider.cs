namespace MeticulousResearch.Core.Updates;

/// <summary>
/// Supplies the latest advertised application version from the configured update source
/// (update-notice/phase.md, SPEC §8). Only a thin adapter actually performs the network fetch; the
/// comparison logic in <see cref="IUpdateService"/> stays window- and network-free so it is
/// <c>@unit</c>-testable with a fake provider (TESTING-STRATEGY §4, mirroring <c>FakeChatService</c>).
/// </summary>
public interface ILatestVersionProvider
{
    /// <summary>
    /// Returns the latest advertised version string, or <c>null</c>/an unreadable value when it
    /// cannot be determined. Failures (offline, unreachable, bad response) surface as an exception
    /// or a <c>null</c> result; the service swallows both to "no update" (SPEC §7.5, §9.1(10)).
    /// </summary>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    Task<string?> GetLatestVersionAsync(CancellationToken cancellationToken = default);
}
