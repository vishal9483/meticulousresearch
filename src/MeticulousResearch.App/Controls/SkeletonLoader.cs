using System.Windows;
using System.Windows.Controls;

namespace MeticulousResearch.App.Controls;

/// <summary>
/// The shared skeleton-loader control (SPEC §3.7): shimmer placeholder rows shown while async data
/// loads, so a pane is never blank during a load. Purely visual; the <em>presence</em> of a loading
/// state is observable via <see cref="ViewModels.StatefulViewModel.IsLoading"/>. Styled via
/// design-system-theming tokens (see Theme/Controls.xaml).
/// </summary>
public sealed class SkeletonLoader : Control
{
    static SkeletonLoader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SkeletonLoader), new FrameworkPropertyMetadata(typeof(SkeletonLoader)));
    }

    /// <summary>How many shimmer placeholder rows to render.</summary>
    public static readonly DependencyProperty RowCountProperty = DependencyProperty.Register(
        nameof(RowCount), typeof(int), typeof(SkeletonLoader), new PropertyMetadata(3));

    /// <summary>The number of shimmer placeholder rows.</summary>
    public int RowCount
    {
        get => (int)GetValue(RowCountProperty);
        set => SetValue(RowCountProperty, value);
    }
}
