namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Resolves the active <see cref="IChatService"/> from the persisted backend preference (SPEC §7.2).
/// The sidecar is the default; the rest of the app consumes the returned service without knowing
/// which backend it is.
/// </summary>
public interface IChatBackendFactory
{
    /// <summary>The backend currently selected in settings (defaults to the sidecar).</summary>
    ChatBackendKind Active { get; }

    /// <summary>Returns the active backend as an <see cref="IChatService"/>.</summary>
    IChatService Resolve();
}
