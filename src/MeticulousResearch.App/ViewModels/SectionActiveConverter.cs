using System.Globalization;
using System.Windows.Data;

namespace MeticulousResearch.App.ViewModels;

/// <summary>
/// Returns <c>true</c> when a nav item's <see cref="Navigation.NavigationSection"/> (value 0)
/// equals the workspace's active section (value 1), so the selected left-nav item is visually
/// marked active (@ui scenario: "the selected nav item is visually marked active").
/// </summary>
public sealed class SectionActiveConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length == 2 && Equals(values[0], values[1]);

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
