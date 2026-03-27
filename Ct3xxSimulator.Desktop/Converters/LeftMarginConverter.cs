// Provides Left Margin Converter for the desktop application value conversion support.
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ct3xxSimulator.Desktop.Converters;

/// <summary>
/// Represents the left margin converter.
/// </summary>
public class LeftMarginConverter : IValueConverter
{
    /// <summary>
    /// Executes convert.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var left = value is double d ? d : 0d;
        return new Thickness(left, 0, 0, 0);
    }

    /// <summary>
    /// Converts the back.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Thickness thickness)
        {
            return thickness.Left;
        }

        return 0d;
    }
}
