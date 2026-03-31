// Provides Test Details View Model for the desktop application view model support.
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

/// <summary>
/// Represents the test details view model.
/// </summary>
public class TestDetailsViewModel : INotifyPropertyChanged
{
    private const double DefaultChartWidth = 240;
    private const double WaveformWidth = 360;
    private const double WaveformHeight = 140;
    private PointCollection _waveformDisplayPoints = new();
    private string? _waveformSummary;

    /// <summary>
    /// Gets the mode.
    /// </summary>
    public string? Mode { get; set; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public string? Message { get; set; }
    /// <summary>
    /// Gets the description.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Gets the display mode name.
    /// </summary>
    public string? DisplayModeName { get; set; }
    /// <summary>
    /// Gets a value indicating whether the am2 test condition is met.
    /// </summary>
    public bool IsAm2Test { get; set; }
    /// <summary>
    /// Gets a value indicating whether the ict test condition is met.
    /// </summary>
    public bool IsIctTest { get; set; }
    /// <summary>
    /// Gets the library name.
    /// </summary>
    public string? LibraryName { get; set; }
    /// <summary>
    /// Gets the function name.
    /// </summary>
    public string? FunctionName { get; set; }
    /// <summary>
    /// Gets the external file.
    /// </summary>
    public string? ExternalFile { get; set; }
    /// <summary>
    /// Gets the chart width.
    /// </summary>
    public double ChartWidth => DefaultChartWidth;

    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<string> Options { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
    public ObservableCollection<TestRecordMetricViewModel> Records { get; } = new();
    /// <summary>
    /// Executes new.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether the options condition is met.
    /// </summary>
    public bool HasOptions => Options.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the records condition is met.
    /// </summary>
    public bool HasRecords => Records.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the limit records condition is met.
    /// </summary>
    public bool HasLimitRecords => Records.Any(r => r.HasLimits);
    /// <summary>
    /// Gets the limit records.
    /// </summary>
    public IEnumerable<TestRecordMetricViewModel> LimitRecords => Records.Where(r => r.HasLimits);
    /// <summary>
    /// Gets a value indicating whether the waveform condition is met.
    /// </summary>
    public bool HasWaveform => WaveformDisplayPoints.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the display mode condition is met.
    /// </summary>
    public bool HasDisplayMode => !string.IsNullOrWhiteSpace(DisplayModeName);
    /// <summary>
    /// Gets a value indicating whether the display options condition is met.
    /// </summary>
    public bool HasDisplayOptions => DisplayOptions.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the library condition is met.
    /// </summary>
    public bool HasLibrary => !string.IsNullOrWhiteSpace(LibraryName);
    /// <summary>
    /// Gets a value indicating whether the function condition is met.
    /// </summary>
    public bool HasFunction => !string.IsNullOrWhiteSpace(FunctionName);
    /// <summary>
    /// Gets a value indicating whether the external file condition is met.
    /// </summary>
    public bool HasExternalFile => !string.IsNullOrWhiteSpace(ExternalFile);

    /// <summary>
    /// Gets the limit range summary.
    /// </summary>
    public string LimitRangeSummary => double.IsNaN(MinLimit) || double.IsNaN(MaxLimit)
        ? string.Empty
        : $"{MinLimit:0.###} .. {MaxLimit:0.###}";

    /// <summary>
    /// Gets the min limit.
    /// </summary>
    public double MinLimit { get; private set; } = double.NaN;
    /// <summary>
    /// Gets the max limit.
    /// </summary>
    public double MaxLimit { get; private set; } = double.NaN;

    /// <summary>
    /// Executes finalize records.
    /// </summary>
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

    /// <summary>
    /// Occurs when PropertyChanged is raised.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Executes OnPropertyChanged.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the waveform.
    /// </summary>
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

/// <summary>
/// Represents the test record metric view model.
/// </summary>
public class TestRecordMetricViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestRecordMetricViewModel"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Gets the destination.
    /// </summary>
    public string? Destination { get; }
    /// <summary>
    /// Gets the expression.
    /// </summary>
    public string? Expression { get; }
    /// <summary>
    /// Gets the unit.
    /// </summary>
    public string? Unit { get; }
    /// <summary>
    /// Gets the record type.
    /// </summary>
    public string? RecordType { get; }
    /// <summary>
    /// Gets the additional info.
    /// </summary>
    public string? AdditionalInfo { get; }
    /// <summary>
    /// Gets the lower.
    /// </summary>
    public double? Lower { get; }
    /// <summary>
    /// Gets the upper.
    /// </summary>
    public double? Upper { get; }

    /// <summary>
    /// Gets a value indicating whether the limits condition is met.
    /// </summary>
    public bool HasLimits => Lower.HasValue || Upper.HasValue;

    /// <summary>
    /// Gets the destination display.
    /// </summary>
    public string DestinationDisplay => string.IsNullOrWhiteSpace(Destination) ? "-" : Destination;
    /// <summary>
    /// Gets the expression display.
    /// </summary>
    public string ExpressionDisplay => string.IsNullOrWhiteSpace(Expression) ? "-" : Expression;
    /// <summary>
    /// Gets the record type display.
    /// </summary>
    public string RecordTypeDisplay => string.IsNullOrWhiteSpace(RecordType) ? "-" : RecordType;
    /// <summary>
    /// Gets the limits display.
    /// </summary>
    public string LimitsDisplay => HasLimits
        ? $"{FormatDouble(Lower)} .. {FormatDouble(Upper)}"
        : "-";

    /// <summary>
    /// Gets the range offset.
    /// </summary>
    public double RangeOffset { get; private set; }
    /// <summary>
    /// Gets the range width.
    /// </summary>
    public double RangeWidth { get; private set; }
    /// <summary>
    /// Gets the chart width.
    /// </summary>
    public double ChartWidth { get; private set; }

    /// <summary>
    /// Gets the limit values.
    /// </summary>
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

    /// <summary>
    /// Executes UpdateNormalization.
    /// </summary>
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

    /// <summary>
    /// Executes FormatDouble.
    /// </summary>
    private static string FormatDouble(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";
    }

    /// <summary>
    /// Executes Clamp.
    /// </summary>
    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}

/// <summary>
/// Represents the display option view model.
/// </summary>
public class DisplayOptionViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayOptionViewModel"/> class.
    /// </summary>
    public DisplayOptionViewModel(string name, string value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the value.
    /// </summary>
    public string Value { get; }
}
