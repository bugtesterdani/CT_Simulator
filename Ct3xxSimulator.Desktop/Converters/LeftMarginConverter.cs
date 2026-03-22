using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ct3xxSimulator.Desktop.Converters;

public class LeftMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var left = value is double d ? d : 0d;
        return new Thickness(left, 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Thickness thickness)
        {
            return thickness.Left;
        }

        return 0d;
    }
}
