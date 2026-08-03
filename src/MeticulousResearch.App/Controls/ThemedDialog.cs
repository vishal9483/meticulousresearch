using System.Windows;
using System.Windows.Controls;

namespace MeticulousResearch.App.Controls;

/// <summary>
/// A styled modal dialog surface for the design-system kit. Downstream features host content in
/// it so dialogs pick up the themed chrome automatically (design-system-theming/phase.md #5).
/// </summary>
public sealed class ThemedDialog : ContentControl
{
    static ThemedDialog()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ThemedDialog), new FrameworkPropertyMetadata(typeof(ThemedDialog)));
    }

    /// <summary>The dialog's heading text.</summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ThemedDialog),
            new PropertyMetadata(string.Empty));

    /// <summary>The dialog's heading text.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
