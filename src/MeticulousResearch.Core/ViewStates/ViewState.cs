namespace MeticulousResearch.Core.ViewStates;

/// <summary>
/// The presentation state a list/view can be in (SPEC §3.7). Views bind to this — never to raw
/// collections — so every pane renders a designed empty, loading, content, or error surface and
/// never a blank screen or a raw stack trace. Owned by the <c>empty-loading-error-states</c> feature
/// and reused by every downstream view.
/// </summary>
public enum ViewState
{
    /// <summary>An async operation is in flight; the view shows a skeleton loader.</summary>
    Loading,

    /// <summary>The operation completed with no items; the view shows a designed empty state + CTA.</summary>
    Empty,

    /// <summary>The operation completed with data; the view shows its content.</summary>
    Content,

    /// <summary>The operation failed; the view shows a human-readable error + recovery action.</summary>
    Error,
}
