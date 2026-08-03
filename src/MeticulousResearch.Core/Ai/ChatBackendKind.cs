namespace MeticulousResearch.Core.Ai;

/// <summary>Which generation backend is active behind <see cref="IChatService"/> (SPEC §7.2).</summary>
public enum ChatBackendKind
{
    /// <summary>The Agent SDK sidecar over a loopback WebSocket — the primary path and the default.</summary>
    Sidecar,

    /// <summary>The pure-C# direct Anthropic Messages API client — the no-sidecar fallback.</summary>
    DirectApi,
}
