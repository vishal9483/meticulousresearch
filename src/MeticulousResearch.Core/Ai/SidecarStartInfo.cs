namespace MeticulousResearch.Core.Ai;

/// <summary>
/// The information needed to launch a sidecar process. Note the API key is <b>not</b> present here:
/// it is never placed on the command line and is instead delivered over the authenticated channel
/// with each request (SPEC §7.2, §7.5). The resolved base URL is provided so the sidecar's Agent SDK
/// targets the same endpoint as the direct-API backend.
/// </summary>
/// <param name="BaseUrl">The resolved effective base URL for the sidecar to target.</param>
public sealed record SidecarStartInfo(string BaseUrl);
