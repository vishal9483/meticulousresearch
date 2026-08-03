using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeticulousResearch.App.Controls;

/// <summary>
/// The shared designed error-state control (SPEC §3.7): a human-readable message and a recovery
/// button — never a raw stack trace. Every view reuses this so a failure is always actionable.
/// Styled via design-system-theming tokens (see Theme/Controls.xaml).
/// </summary>
public sealed class ErrorState : Control
{
    static ErrorState()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ErrorState), new FrameworkPropertyMetadata(typeof(ErrorState)));
    }

    /// <summary>The human-readable error message (never a raw exception detail).</summary>
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(ErrorState), new PropertyMetadata(string.Empty));

    /// <summary>The label of the recovery-action button (e.g. "Retry").</summary>
    public static readonly DependencyProperty RecoveryActionProperty = DependencyProperty.Register(
        nameof(RecoveryAction), typeof(string), typeof(ErrorState), new PropertyMetadata(string.Empty));

    /// <summary>The command invoked by the recovery-action button.</summary>
    public static readonly DependencyProperty RecoveryCommandProperty = DependencyProperty.Register(
        nameof(RecoveryCommand), typeof(ICommand), typeof(ErrorState), new PropertyMetadata(null));

    /// <summary>The human-readable error message.</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>The recovery-action button label.</summary>
    public string RecoveryAction
    {
        get => (string)GetValue(RecoveryActionProperty);
        set => SetValue(RecoveryActionProperty, value);
    }

    /// <summary>The recovery-action command.</summary>
    public ICommand? RecoveryCommand
    {
        get => (ICommand?)GetValue(RecoveryCommandProperty);
        set => SetValue(RecoveryCommandProperty, value);
    }
}
