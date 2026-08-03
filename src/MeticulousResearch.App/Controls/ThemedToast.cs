using System.Windows;
using System.Windows.Controls;

namespace MeticulousResearch.App.Controls;

/// <summary>
/// A styled transient toast/notification for the design-system kit. Downstream features raise it
/// so notifications pick up the themed chrome automatically (design-system-theming/phase.md #5).
/// </summary>
public sealed class ThemedToast : ContentControl
{
    static ThemedToast()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ThemedToast), new FrameworkPropertyMetadata(typeof(ThemedToast)));
    }

    /// <summary>The toast severity, used to pick the semantic accent (info/success/warning/error).</summary>
    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(nameof(Severity), typeof(string), typeof(ThemedToast),
            new PropertyMetadata("info"));

    /// <summary>The toast severity (info/success/warning/error).</summary>
    public string Severity
    {
        get => (string)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }
}
