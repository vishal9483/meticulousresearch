using System.Windows;

namespace MeticulousResearch.App.Services;

/// <summary>
/// The WPF <see cref="IClipboardService"/> backed by <see cref="Clipboard"/>. Used by the turn "Copy"
/// action to place an assistant turn's text on the system clipboard (SPEC §3.3).
/// </summary>
public sealed class WpfClipboardService : IClipboardService
{
    /// <inheritdoc />
    public void SetText(string text) => Clipboard.SetText(text ?? "");
}
