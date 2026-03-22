using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Ct3xxSimulator.Desktop.ViewModels;

public class TestDetailsViewModel : INotifyPropertyChanged
{
    private const double DefaultChartWidth = 240;
    private const double WaveformWidth = 360;
    private const double WaveformHeight = 140;
    private PointCollection _waveformDisplayPoints = new();
    private string? _waveformSummary;

    public string? Mode { get; set; }
    public string? Message { get; set; }
    public string? Description { get; set; }
    public string? DisplayModeName { get; set; }
    public bool IsAm2Test { get; set; }
    public bool IsIctTest { get; set; }
    public string? LibraryName { get; set; }
    public string? FunctionName { get; set; }
    public string? ExternalFile { get; set; }
    public double ChartWidth => DefaultChartWidth;

    public ObservableCollection<string> Options { get; } = new();
    public ObservableCollection<TestRecordMetricViewModel> Records { get; } = new();
    public ObservableCollection<DisplayOptionViewModel> DisplayOptions { get; } = new();
    public PointCollection WaveformDisplayPoints
    {
        get => _waveformDisplayPoints;
        private set
        {
            if (_waveformDisplayPoints != value)
            {
                _waveformDisplayPoints = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasWaveform));
            }
        }
    }

    public string? WaveformSummary
    {
        get => _waveformSummary;
        private set
        {
            if (_waveformSummary != value)
            {
                _waveformSummary = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasOptions => Options.Count > 0;
    public bool HasRecords => Records.Count > 0;
    public bool HasLimitRecords => Records.Any(r => r.HasLimits);
    public IEnumerable<TestRecordMetricViewModel> LimitRecords => Records.Where(r => r.HasLimits);
    public bool HasWaveform => WaveformDisplayPoints.Count > 0;
    public bool HasDisplayMode => !string.IsNullOrWhiteSpace(DisplayModeName);
    public bool HasDisplayOptions => DisplayOptions.Count > 0;
    public bool HasLibrary => !string.IsNullOrWhiteSpace(LibraryName);
    public bool HasFunction => !string.IsNullOrWhiteSpace(FunctionName);
    public bool HasExternalFile => !string.IsNullOrWhiteSpace(ExternalFile);

    public string LimitRangeSummary => double.IsNaN(MinLimit) || double.IsNaN(MaxLimit)
        ? string.Empty
        : $"{MinLimit:0.###} .. {MaxLimit:0.###}";

    public double MinLimit { get; private set; } = double.NaN;
    public double MaxLimit { get; private set; } = double.NaN;

    public void FinalizeRecords()
    {
        var limits = Records
            .Where(r => r.HasLimits)
            .SelectMany(r => r.GetLimitValues())
            .ToList();

        if (limits.Count >= 1)
        {
            MinLimit = limits.Min();
            MaxLimit = limits.Max();
            if (Math.Abs(MaxLimit - MinLimit) < double.Epsilon)
            {
                MaxLimit = MinLimit + 1; // avoid zero range
            }
        }
        else
        {
            MinLimit = MaxLimit = double.NaN;
        }

        foreach (var record in Records)
        {
            record.UpdateNormalization(MinLimit, MaxLimit, ChartWidth);
        }

        OnPropertyChanged(nameof(HasOptions));
        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(HasLimitRecords));
        OnPropertyChanged(nameof(LimitRecords));
        OnPropertyChanged(nameof(LimitRangeSummary));
        OnPropertyChanged(nameof(HasDisplayOptions));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void SetWaveform(IEnumerable<Point> samples, string? unit)
    {
        var list = samples.ToList();
        if (list.Count == 0)
        {
            WaveformDisplayPoints = new PointCollection();
            WaveformSummary = null;
            return;
        }

        var minX = list.Min(p => p.X);
        var maxX = list.Max(p => p.X);
        var minY = list.Min(p => p.Y);
        var maxY = list.Max(p => p.Y);
        if (Math.Abs(maxX - minX) < double.Epsilon)
        {
            maxX = minX + 1;
        }

        if (Math.Abs(maxY - minY) < double.Epsilon)
        {
            maxY += 1;
            minY -= 1;
        }

        var collection = new PointCollection();
        foreach (var point in list)
        {
            var nx = (point.X - minX) / (maxX - minX);
            var ny = (point.Y - minY) / (maxY - minY);
            var px = nx * WaveformWidth;
            var py = WaveformHeight - (ny * WaveformHeight);
            collection.Add(new Point(px, py));
        }

        WaveformDisplayPoints = collection;
        WaveformSummary = $"Amplitude: {minY:0.###} .. {maxY:0.###} {unit}";
    }
}

public class TestRecordMetricViewModel
{
    public TestRecordMetricViewModel(string title,
                                     string? destination,
                                     string? expression,
                                     string? unit,
                                     string? recordType,
                                     string? additionalInfo,
                                     double? lower,
                                     double? upper)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Parameter" : title;
        Destination = string.IsNullOrWhiteSpace(destination) ? null : destination;
        Expression = string.IsNullOrWhiteSpace(expression) ? null : expression;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit;
        RecordType = string.IsNullOrWhiteSpace(recordType) ? null : recordType;
        AdditionalInfo = additionalInfo;
        Lower = lower;
        Upper = upper;
    }

    public string Title { get; }
    public string? Destination { get; }
    public string? Expression { get; }
    public string? Unit { get; }
    public string? RecordType { get; }
    public string? AdditionalInfo { get; }
    public double? Lower { get; }
    public double? Upper { get; }

    public bool HasLimits => Lower.HasValue || Upper.HasValue;

    public string DestinationDisplay => string.IsNullOrWhiteSpace(Destination) ? "-" : Destination;
    public string ExpressionDisplay => string.IsNullOrWhiteSpace(Expression) ? "-" : Expression;
    public string RecordTypeDisplay => string.IsNullOrWhiteSpace(RecordType) ? "-" : RecordType;
    public string LimitsDisplay => HasLimits
        ? $"{FormatDouble(Lower)} .. {FormatDouble(Upper)}"
        : "-";

    public double RangeOffset { get; private set; }
    public double RangeWidth { get; private set; }
    public double ChartWidth { get; private set; }

    public IEnumerable<double> GetLimitValues()
    {
        if (Lower.HasValue)
        {
            yield return Lower.Value;
        }

        if (Upper.HasValue)
        {
            yield return Upper.Value;
        }
    }

    internal void UpdateNormalization(double min, double max, double chartWidth)
    {
        ChartWidth = chartWidth;
        if (!HasLimits || double.IsNaN(min) || double.IsNaN(max))
        {
            RangeOffset = 0;
            RangeWidth = 0;
            return;
        }

        var lower = Lower ?? min;
        var upper = Upper ?? lower;
        var range = max - min;
        if (range <= double.Epsilon)
        {
            RangeOffset = chartWidth * 0.25;
            RangeWidth = chartWidth * 0.5;
            return;
        }

        var normalizedLower = (lower - min) / range;
        var normalizedUpper = (upper - min) / range;
        normalizedLower = Clamp(normalizedLower, 0, 1);
        normalizedUpper = Clamp(normalizedUpper, 0, 1);
        var width = Math.Abs(normalizedUpper - normalizedLower) * chartWidth;
        RangeOffset = normalizedLower * chartWidth;
        RangeWidth = Math.Max(2, width);
    }

    private static string FormatDouble(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}

public class DisplayOptionViewModel
{
    public DisplayOptionViewModel(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public string Value { get; }
}
