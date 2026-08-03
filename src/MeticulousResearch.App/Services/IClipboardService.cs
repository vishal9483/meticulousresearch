namespace MeticulousResearch.App.Services;

/// <summary>
/// The clipboard seam used by turn actions (SPEC §3.3 "Copy"). Abstracted so the copy command is
/// <c>@unit</c>-testable without a WPF/STA message pump; the real implementation delegates to the
/// system clipboard.
/// </summary>
public interface IClipboardService
{
    /// <summary>Places <paramref name="text"/> on the system clipboard.</summary>
    /// <param name="text">The text to copy (an empty string is allowed).</param>
    void SetText(string text);
}
