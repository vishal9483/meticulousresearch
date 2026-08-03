namespace MeticulousResearch.App.Services;

/// <summary>
/// First-run contextual-hint state (SPEC §3.8(5)). After onboarding finishes, the app lands on the
/// Projects home and shows brief hints on the primary actions; this holds whether those hints are
/// pending so the home view can render them once and then dismiss. Window-free so the finish flow
/// is <c>@unit</c>-testable.
/// </summary>
public interface IFirstRunHints
{
    /// <summary>True when first-run hints should be shown on the Projects home.</summary>
    bool ArePending { get; }

    /// <summary>Requests that first-run hints be shown (called when onboarding finishes).</summary>
    void Request();

    /// <summary>Clears the pending hints once they have been shown.</summary>
    void Dismiss();
}

/// <summary>Default in-memory <see cref="IFirstRunHints"/>.</summary>
public sealed class FirstRunHints : IFirstRunHints
{
    /// <inheritdoc />
    public bool ArePending { get; private set; }

    /// <inheritdoc />
    public void Request() => ArePending = true;

    /// <inheritdoc />
    public void Dismiss() => ArePending = false;
}
