using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeticulousResearch.App.Controls;

/// <summary>
/// The shared designed empty-state control (SPEC §3.7): an icon glyph, a message, and a
/// call-to-action button. Every primary list reuses this so no pane is ever blank. Styled via
/// design-system-theming tokens (see Theme/Controls.xaml).
/// </summary>
public sealed class EmptyState : Control
{
    static EmptyState()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(EmptyState), new FrameworkPropertyMetadata(typeof(EmptyState)));
    }

    /// <summary>The empty-state headline message (e.g. "No projects yet").</summary>
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    /// <summary>The label of the call-to-action button (e.g. "New project").</summary>
    public static readonly DependencyProperty CallToActionProperty = DependencyProperty.Register(
        nameof(CallToAction), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    /// <summary>The command invoked by the call-to-action button.</summary>
    public static readonly DependencyProperty CallToActionCommandProperty = DependencyProperty.Register(
        nameof(CallToActionCommand), typeof(ICommand), typeof(EmptyState), new PropertyMetadata(null));

    /// <summary>The empty-state headline message.</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>The call-to-action button label.</summary>
    public string CallToAction
    {
        get => (string)GetValue(CallToActionProperty);
        set => SetValue(CallToActionProperty, value);
    }

    /// <summary>The call-to-action command.</summary>
    public ICommand? CallToActionCommand
    {
        get => (ICommand?)GetValue(CallToActionCommandProperty);
        set => SetValue(CallToActionCommandProperty, value);
    }
}
