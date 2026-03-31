// Provides Evaluation Details Window for the desktop application window logic.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop.Views;

public partial class EvaluationDetailsWindow : Window
{
    private static readonly Regex ChannelScopeRegex = new(@"^CH\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MetricRegex = new(
        @"(?<metric>[A-Za-z0-9_\-\s/]+)\s*=\s*(?<actual>-?[0-9]+(?:[.,][0-9]+)?)\s*(?<unit>[A-Za-z%]+)?(?:\s*\[(?<lower>-?[0-9]+(?:[.,][0-9]+)?)\.\.(?<upper>-?[0-9]+(?:[.,][0-9]+)?)\])?",
        RegexOptions.Compiled);

    private static readonly string[] Palette =
    {
        "#FF2E6F95",
        "#FFBC6C25",
        "#FF5A8F3D",
        "#FF8A3B5A",
        "#FF6B5B95",
        "#FF2F8C7A"
    };

    private StepResultViewModel? _result;
    private bool _updatingSelection;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluationDetailsWindow"/> class.
    /// </summary>
    public EvaluationDetailsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the result.
    /// </summary>
    public void UpdateResult(StepResultViewModel result)
    {
        _result = result;
        TitleTextBlock.Text = result.StepName;
        SubtitleTextBlock.Text = BuildSubtitle(result);

        ResultSourceTextBlock.Text = result.ResultSourceLabel;
        OutcomeTextBlock.Text = result.DisplayOutcome;
        MeasuredValueTextBlock.Text = AppendUnit(result.DisplayMeasuredValue, result.DisplayUnit);
        RangeTextBlock.Text = BuildRangeText(result.DisplayLowerLimit, result.DisplayUpperLimit, result.DisplayUnit);
        DetailsTextBlock.Text = string.IsNullOrWhiteSpace(result.DisplayDetails) ? "-" : result.DisplayDetails;
        OutcomeTextBlock.Foreground = GetOutcomeBrush(result.DisplayOutcome);
        ResultSourceTextBlock.Foreground = GetSourceBrush(result.ResultSourceLabel);

        SummaryOutcomeTextBlock.Text = result.Outcome;
        SummaryMeasuredValueTextBlock.Text = AppendUnit(result.MeasuredValue, result.Unit);
        SummaryLowerLimitTextBlock.Text = AppendUnit(result.LowerLimit, result.Unit);
        SummaryUpperLimitTextBlock.Text = AppendUnit(result.UpperLimit, result.Unit);
        SummaryDetailsTextBlock.Text = string.IsNullOrWhiteSpace(result.Details) ? "-" : result.Details;
        RawDetailsTextBlock.Text = string.IsNullOrWhiteSpace(result.Details) ? "-" : result.Details;
        MetricsDataGrid.ItemsSource = BuildMetricRows(result.Outcome, result.MeasuredValue, result.LowerLimit, result.UpperLimit, result.Unit, result.Details).ToList();

        CsvRowTextBlock.Text = result.HasCsvReplayMatch ? result.CsvRowNumber?.ToString(CultureInfo.InvariantCulture) ?? "-" : "-";
        CsvOutcomeTextBlock.Text = string.IsNullOrWhiteSpace(result.CsvOutcome) ? "-" : result.CsvOutcome;
        CsvMeasuredValueTextBlock.Text = string.IsNullOrWhiteSpace(result.CsvMeasuredValue) ? "-" : result.CsvMeasuredValue;
        CsvRangeTextBlock.Text = BuildRangeText(result.CsvLowerLimit, result.CsvUpperLimit, string.Empty);
        CsvDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(result.CsvDescription) ? "-" : result.CsvDescription;
        CsvMessageTextBlock.Text = string.IsNullOrWhiteSpace(result.CsvMessage) ? "-" : result.CsvMessage;
        CsvRawDetailsTextBlock.Text = BuildCsvRawText(result);
        CsvMetricsDataGrid.ItemsSource = BuildMetricRows(result.CsvOutcome, result.CsvMeasuredValue, result.CsvLowerLimit, result.CsvUpperLimit, string.Empty, BuildCsvDetails(result), fallbackScope: "CSV").ToList();

        CompareOutcomeSimTextBlock.Text = result.Outcome;
        CompareOutcomeCsvTextBlock.Text = FormatFallback(result.CsvOutcome);
        CompareMeasuredSimTextBlock.Text = AppendUnit(result.MeasuredValue, result.Unit);
        CompareMeasuredCsvTextBlock.Text = FormatFallback(result.CsvMeasuredValue);
        CompareRangeSimTextBlock.Text = BuildRangeText(result.LowerLimit, result.UpperLimit, result.Unit);
        CompareRangeCsvTextBlock.Text = BuildRangeText(result.CsvLowerLimit, result.CsvUpperLimit, string.Empty);
        CompareDetailsSimTextBlock.Text = string.IsNullOrWhiteSpace(result.Details) ? "-" : result.Details;
        CompareDetailsCsvTextBlock.Text = BuildCsvDetails(result);
        ComparisonSummaryTextBlock.Text = result.HasCsvReplayMatch ? result.ComparisonSummary : "Kein CSV-Match fuer diesen Schritt vorhanden.";
        ComparisonModeTextBlock.Text = BuildComparisonModeText(result);

        var labels = result.CurvePoints
            .Select(point => point.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _updatingSelection = true;
        SeriesListBox.ItemsSource = labels;
        SeriesListBox.SelectedItems.Clear();
        foreach (var label in labels)
        {
            SeriesListBox.SelectedItems.Add(label);
        }
        _updatingSelection = false;

        ReplayTabControl.SelectedIndex = result.HasCsvReplayMatch ? 2 : 0;
        RenderChart();
    }

    /// <summary>
    /// Executes BuildMetricRows.
    /// </summary>
    private static IEnumerable<MetricDetailRow> BuildMetricRows(string outcome, string measuredValue, string lowerLimit, string upperLimit, string unit, string? details, string fallbackScope = "Schritt")
    {
        var (summaryPrefix, metricDetails) = SplitDetailsSections(details);
        var primaryScope = GetPrimaryScope(metricDetails, fallbackScope);
        var rows = new List<MetricDetailRow>
        {
            new()
            {
                Scope = "Gesamt",
                Metric = "Ergebnis",
                Actual = FormatFallback(outcome),
                LowerLimit = "-",
                UpperLimit = "-",
                Unit = string.Empty,
                Status = FormatFallback(outcome),
                Note = "Gesamtergebnis"
            },
            new()
            {
                Scope = primaryScope,
                Metric = "Ist",
                Actual = FormatFallback(measuredValue),
                LowerLimit = FormatLimitText(lowerLimit),
                UpperLimit = FormatLimitText(upperLimit),
                Unit = unit,
                Status = ComputeStatus(TryParseNumeric(measuredValue), TryParseNumeric(lowerLimit), TryParseNumeric(upperLimit), outcome),
                Note = "Hauptwert"
            }
        };

        if (!string.IsNullOrWhiteSpace(summaryPrefix))
        {
            rows.Add(new MetricDetailRow
            {
                Scope = "Signalpfade",
                Metric = "Zuordnung",
                Actual = "-",
                LowerLimit = "-",
                UpperLimit = "-",
                Unit = string.Empty,
                Status = string.Empty,
                Note = summaryPrefix
            });
        }

        if (string.IsNullOrWhiteSpace(metricDetails))
        {
            return rows;
        }

        foreach (var scopePart in metricDetails.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var scope = fallbackScope;
            var metricsText = scopePart;
            var separatorIndex = scopePart.IndexOf(':');
            if (separatorIndex >= 0)
            {
                scope = scopePart[..separatorIndex].Trim();
                metricsText = scopePart[(separatorIndex + 1)..];
            }

            foreach (var metricPart in metricsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var match = MetricRegex.Match(metricPart);
                if (match.Success)
                {
                    var actualText = match.Groups["actual"].Value.Replace(',', '.');
                    var lowerText = match.Groups["lower"].Success ? match.Groups["lower"].Value.Replace(',', '.') : string.Empty;
                    var upperText = match.Groups["upper"].Success ? match.Groups["upper"].Value.Replace(',', '.') : string.Empty;
                    rows.Add(new MetricDetailRow
                    {
                        Scope = scope,
                        Metric = match.Groups["metric"].Value.Trim(),
                        Actual = match.Groups["actual"].Value.Trim(),
                        LowerLimit = string.IsNullOrWhiteSpace(lowerText) ? "-" : lowerText,
                        UpperLimit = string.IsNullOrWhiteSpace(upperText) ? "-" : upperText,
                        Unit = match.Groups["unit"].Value.Trim(),
                        Status = ComputeStatus(TryParseNumeric(actualText), TryParseNumeric(lowerText), TryParseNumeric(upperText), null),
                        Note = match.Groups["lower"].Success || match.Groups["upper"].Success ? "Grenzwert geprueft" : string.Empty
                    });
                }
                else
                {
                    rows.Add(new MetricDetailRow
                    {
                        Scope = scope,
                        Metric = metricPart.Trim(),
                        Actual = "-",
                        LowerLimit = "-",
                        UpperLimit = "-",
                        Unit = string.Empty,
                        Status = string.Empty,
                        Note = "Freitext"
                    });
                }
            }
        }

        return rows;
    }

    /// <summary>
    /// Executes BuildSubtitle.
    /// </summary>
    private static string BuildSubtitle(StepResultViewModel result)
    {
        if (!result.HasCsvReplayMatch)
        {
            return "Auswertung des ausgewaehlten Testschritts mit Soll-/Ist-Grenzen und verfuegbaren Kurven.";
        }

        return $"Auswertung des ausgewaehlten Testschritts. Anzeigequelle: {result.ResultSourceLabel}. CSV-Zeile {result.CsvRowNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"}.";
    }

    /// <summary>
    /// Executes BuildComparisonModeText.
    /// </summary>
    private static string BuildComparisonModeText(StepResultViewModel result)
    {
        return result.CsvDisplayMode.ToUpperInvariant() switch
        {
            "COMPARE" => "Der Schritt wird simulatorseitig ausgewertet; CSV wird parallel zum Vergleich angezeigt.",
            "CSVDRIVESRESULT" => "Die Hauptanzeige folgt CSV-Ergebniswerten; Pfade, Kurven und technische Zustandsdaten bleiben simulatorseitig.",
            _ => "Es ist kein CSV-Vergleichsmodus aktiv."
        };
    }

    /// <summary>
    /// Executes BuildCsvDetails.
    /// </summary>
    private static string BuildCsvDetails(StepResultViewModel result)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.CsvDescription))
        {
            parts.Add(result.CsvDescription);
        }

        if (!string.IsNullOrWhiteSpace(result.CsvMessage))
        {
            parts.Add(result.CsvMessage);
        }

        if (!string.IsNullOrWhiteSpace(result.CsvMatchReason))
        {
            parts.Add($"Match: {result.CsvMatchReason}");
        }

        return parts.Count == 0 ? "-" : string.Join(" | ", parts);
    }

    /// <summary>
    /// Executes BuildCsvRawText.
    /// </summary>
    private static string BuildCsvRawText(StepResultViewModel result)
    {
        if (!result.HasCsvReplayMatch)
        {
            return "-";
        }

        return $"CSV-Zeile: {result.CsvRowNumber?.ToString(CultureInfo.InvariantCulture) ?? "-"}\n" +
               $"Beschreibung: {FormatFallback(result.CsvDescription)}\n" +
               $"Message: {FormatFallback(result.CsvMessage)}\n" +
               $"Ergebnis: {FormatFallback(result.CsvOutcome)}\n" +
               $"Wert: {FormatFallback(result.CsvMeasuredValue)}\n" +
               $"Soll Min: {FormatFallback(result.CsvLowerLimit)}\n" +
               $"Soll Max: {FormatFallback(result.CsvUpperLimit)}\n" +
               $"Match: {FormatFallback(result.CsvMatchReason)}";
    }

    /// <summary>
    /// Initializes a new instance of static.
    /// </summary>
    private static (string SummaryPrefix, string MetricDetails) SplitDetailsSections(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return (string.Empty, string.Empty);
        }

        var separatorIndex = details.IndexOf('|');
        if (separatorIndex < 0)
        {
            return (string.Empty, details);
        }

        return (details[..separatorIndex].Trim(), details[(separatorIndex + 1)..].Trim());
    }

    /// <summary>
    /// Executes GetPrimaryScope.
    /// </summary>
    private static string GetPrimaryScope(string? details, string fallbackScope)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return fallbackScope;
        }

        foreach (var scopePart in details.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = scopePart.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var scope = scopePart[..separatorIndex].Trim();
            if (ChannelScopeRegex.IsMatch(scope))
            {
                return scope.ToUpperInvariant();
            }
        }

        return fallbackScope;
    }

    /// <summary>
    /// Executes OnSeriesSelectionChanged.
    /// </summary>
    private void OnSeriesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection)
        {
            return;
        }

        RenderChart();
    }

    /// <summary>
    /// Executes RenderChart.
    /// </summary>
    private void RenderChart()
    {
        ChartCanvas.Children.Clear();
        ChartCanvas.Width = 820;
        ChartCanvas.Height = 420;

        if (_result == null)
        {
            return;
        }

        var selectedLabels = SeriesListBox.SelectedItems
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedSeries = _result.CurvePoints
            .Where(point => point.Value.HasValue && (selectedLabels.Count == 0 || selectedLabels.Contains(point.Label)))
            .GroupBy(point => point.Label, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedSeries.Count == 0)
        {
            ChartCanvas.Children.Add(new TextBlock
            {
                Text = "Keine Kurvendaten fuer die aktuelle Auswahl vorhanden.",
                Foreground = Brush("#FF52606D"),
                FontSize = 16
            });
            StatusTextBlock.Text = "Keine Kurve verfuegbar.";
            return;
        }

        const double left = 52;
        const double top = 22;
        const double width = 700;
        const double height = 280;
        ChartCanvas.Width = width + 120;
        ChartCanvas.Height = 380;

        var points = selectedSeries.SelectMany(group => group).ToList();
        var minTime = points.Min(point => point.TimeMs);
        var maxTime = Math.Max(minTime + 1, points.Max(point => point.TimeMs));
        var minValue = points.Min(point => point.Value!.Value);
        var maxValue = points.Max(point => point.Value!.Value);

        var lowerLimit = TryParseNumeric(_result.LowerLimit);
        var upperLimit = TryParseNumeric(_result.UpperLimit);
        if (lowerLimit.HasValue)
        {
            minValue = Math.Min(minValue, lowerLimit.Value);
            maxValue = Math.Max(maxValue, lowerLimit.Value);
        }

        if (upperLimit.HasValue)
        {
            minValue = Math.Min(minValue, upperLimit.Value);
            maxValue = Math.Max(maxValue, upperLimit.Value);
        }

        if (Math.Abs(maxValue - minValue) < double.Epsilon)
        {
            maxValue = minValue + 1;
        }

        ChartCanvas.Children.Add(new Line { X1 = left, Y1 = top + height, X2 = left + width, Y2 = top + height, Stroke = Brush("#FF768191"), StrokeThickness = 1.4 });
        ChartCanvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = top + height, Stroke = Brush("#FF768191"), StrokeThickness = 1.4 });

        if (lowerLimit.HasValue && upperLimit.HasValue)
        {
            DrawLimitBand(lowerLimit.Value, upperLimit.Value, width, height, left, top, minValue, maxValue);
        }

        if (lowerLimit.HasValue)
        {
            DrawLimitLine(lowerLimit.Value, "Soll Min", width, height, left, top, minValue, maxValue, "#FFD97706");
        }

        if (upperLimit.HasValue)
        {
            DrawLimitLine(upperLimit.Value, "Soll Max", width, height, left, top, minValue, maxValue, "#FFD97706");
        }

        for (var index = 0; index < selectedSeries.Count; index++)
        {
            var color = Palette[index % Palette.Length];
            var group = selectedSeries[index].OrderBy(point => point.TimeMs).ToList();
            var polyline = new Polyline
            {
                Stroke = Brush(color),
                StrokeThickness = 2.4
            };

            foreach (var point in group)
            {
                var x = left + (point.TimeMs - minTime) / (double)(maxTime - minTime) * width;
                var y = top + height - ((point.Value!.Value - minValue) / (maxValue - minValue) * height);
                polyline.Points.Add(new Point(x, y));
                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brush(color),
                    ToolTip = $"{point.Label}: {point.Value?.ToString("0.###", CultureInfo.InvariantCulture)} {point.Unit} @ {point.TimeMs} ms"
                };
                Canvas.SetLeft(dot, x - 4);
                Canvas.SetTop(dot, y - 4);
                ChartCanvas.Children.Add(dot);

                if (IsOutsideLimits(point.Value!.Value, lowerLimit, upperLimit))
                {
                    var marker = new Ellipse
                    {
                        Width = 14,
                        Height = 14,
                        Stroke = Brush("#FFB23A48"),
                        StrokeThickness = 2,
                        Fill = Brushes.Transparent,
                        ToolTip = $"Ausserhalb der Grenze: {point.Label} @ {point.TimeMs} ms"
                    };
                    Canvas.SetLeft(marker, x - 7);
                    Canvas.SetTop(marker, y - 7);
                    ChartCanvas.Children.Add(marker);
                }
            }

            ChartCanvas.Children.Add(polyline);
            DrawLegendEntry(index, color, group[0].Label);
        }

        AddChartLabel(left - 18, top - 4, maxValue.ToString("0.###", CultureInfo.InvariantCulture));
        AddChartLabel(left - 18, top + height - 10, minValue.ToString("0.###", CultureInfo.InvariantCulture));
        AddChartLabel(left, top + height + 8, $"{minTime} ms");
        AddChartLabel(left + width - 42, top + height + 8, $"{maxTime} ms");
        ChartTitleTextBlock.Text = selectedSeries.Count == 1
            ? $"Kurvendiagramm: {selectedSeries[0].Key}"
            : $"Kurvendiagramm: {selectedSeries.Count} Signale";
        var violationCount = points.Count(point => point.Value.HasValue && IsOutsideLimits(point.Value.Value, lowerLimit, upperLimit));
        StatusTextBlock.Text = $"Kurven: {selectedSeries.Count} | Punkte: {points.Count} | Grenzen: {FormatLimit(lowerLimit)} .. {FormatLimit(upperLimit)} | Ausserhalb: {violationCount}";
    }

    /// <summary>
    /// Executes DrawLimitLine.
    /// </summary>
    private void DrawLimitLine(double value, string label, double width, double height, double left, double top, double minValue, double maxValue, string color)
    {
        var y = top + height - ((value - minValue) / (maxValue - minValue) * height);
        var line = new Line
        {
            X1 = left,
            X2 = left + width,
            Y1 = y,
            Y2 = y,
            Stroke = Brush(color),
            StrokeThickness = 1.4,
            StrokeDashArray = new DoubleCollection { 6, 4 }
        };
        ChartCanvas.Children.Add(line);
        AddChartLabel(left + width + 12, y - 8, $"{label}: {value.ToString("0.###", CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// Executes DrawLimitBand.
    /// </summary>
    private void DrawLimitBand(double lowerLimit, double upperLimit, double width, double height, double left, double top, double minValue, double maxValue)
    {
        var upperY = top + height - ((upperLimit - minValue) / (maxValue - minValue) * height);
        var lowerY = top + height - ((lowerLimit - minValue) / (maxValue - minValue) * height);
        var bandTop = Math.Min(upperY, lowerY);
        var bandHeight = Math.Abs(lowerY - upperY);

        var band = new Rectangle
        {
            Width = width,
            Height = bandHeight,
            Fill = Brush("#2A6AA84F"),
            StrokeThickness = 0
        };
        Canvas.SetLeft(band, left);
        Canvas.SetTop(band, bandTop);
        ChartCanvas.Children.Add(band);
        AddChartLabel(left + 8, bandTop + 4, "Zulaessiger Bereich");
    }

    /// <summary>
    /// Executes DrawLegendEntry.
    /// </summary>
    private void DrawLegendEntry(int index, string color, string text)
    {
        var top = 316 + index * 22;
        var swatch = new Rectangle
        {
            Width = 16,
            Height = 10,
            Fill = Brush(color)
        };
        Canvas.SetLeft(swatch, 56);
        Canvas.SetTop(swatch, top + 4);
        ChartCanvas.Children.Add(swatch);

        AddChartLabel(78, top, text);
    }

    /// <summary>
    /// Executes AddChartLabel.
    /// </summary>
    private void AddChartLabel(double left, double top, string text)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Brush("#FF52606D"),
            FontSize = 12
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        ChartCanvas.Children.Add(label);
    }

    /// <summary>
    /// Executes AppendUnit.
    /// </summary>
    private static string AppendUnit(string value, string unit)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-")
        {
            return "-";
        }

        return string.IsNullOrWhiteSpace(unit) ? value : $"{value} {unit}";
    }

    /// <summary>
    /// Executes BuildRangeText.
    /// </summary>
    private static string BuildRangeText(string lowerLimit, string upperLimit, string unit)
    {
        if (string.IsNullOrWhiteSpace(lowerLimit) && string.IsNullOrWhiteSpace(upperLimit))
        {
            return "-";
        }

        var lower = FormatLimitText(lowerLimit);
        var upper = FormatLimitText(upperLimit);
        return string.IsNullOrWhiteSpace(unit) ? $"{lower} .. {upper}" : $"{lower} .. {upper} {unit}";
    }

    /// <summary>
    /// Executes FormatLimitText.
    /// </summary>
    private static string FormatLimitText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    /// <summary>
    /// Executes FormatFallback.
    /// </summary>
    private static string FormatFallback(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    /// <summary>
    /// Executes TryParseNumeric.
    /// </summary>
    private static double? TryParseNumeric(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return value;
        }

        return null;
    }

    /// <summary>
    /// Executes FormatLimit.
    /// </summary>
    private static string FormatLimit(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "-";
    }

    /// <summary>
    /// Executes IsOutsideLimits.
    /// </summary>
    private static bool IsOutsideLimits(double value, double? lowerLimit, double? upperLimit)
    {
        if (lowerLimit.HasValue && value < lowerLimit.Value)
        {
            return true;
        }

        if (upperLimit.HasValue && value > upperLimit.Value)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Executes ComputeStatus.
    /// </summary>
    private static string ComputeStatus(double? actual, double? lowerLimit, double? upperLimit, string? fallbackOutcome)
    {
        if (actual.HasValue)
        {
            if (lowerLimit.HasValue && actual.Value < lowerLimit.Value)
            {
                return "Zu klein";
            }

            if (upperLimit.HasValue && actual.Value > upperLimit.Value)
            {
                return "Zu gross";
            }

            if (lowerLimit.HasValue || upperLimit.HasValue)
            {
                return "OK";
            }
        }

        return string.IsNullOrWhiteSpace(fallbackOutcome) ? string.Empty : fallbackOutcome;
    }

    /// <summary>
    /// Executes GetOutcomeBrush.
    /// </summary>
    private static SolidColorBrush GetOutcomeBrush(string? outcome)
    {
        return (outcome ?? string.Empty).ToUpperInvariant() switch
        {
            "PASS" => Brush("#FF2D6A4F"),
            "FAIL" => Brush("#FFB23A48"),
            "ERROR" => Brush("#FF8B1E3F"),
            _ => Brush("#FF293241")
        };
    }

    /// <summary>
    /// Executes GetSourceBrush.
    /// </summary>
    private static SolidColorBrush GetSourceBrush(string? sourceLabel)
    {
        return (sourceLabel ?? string.Empty).ToUpperInvariant() switch
        {
            "CSV" => Brush("#FF7C5E10"),
            "SIM/CSV" => Brush("#FF375A7F"),
            _ => Brush("#FF2E5E86")
        };
    }

    /// <summary>
    /// Executes OnClose.
    /// </summary>
    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Executes Brush.
    /// </summary>
    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));

    private sealed class MetricDetailRow
    {
        /// <summary>
        /// Gets the scope.
        /// </summary>
        public string Scope { get; init; } = string.Empty;
        /// <summary>
        /// Gets the metric.
        /// </summary>
        public string Metric { get; init; } = string.Empty;
        /// <summary>
        /// Gets the actual.
        /// </summary>
        public string Actual { get; init; } = string.Empty;
        /// <summary>
        /// Gets the lower limit.
        /// </summary>
        public string LowerLimit { get; init; } = string.Empty;
        /// <summary>
        /// Gets the upper limit.
        /// </summary>
        public string UpperLimit { get; init; } = string.Empty;
        /// <summary>
        /// Gets the unit.
        /// </summary>
        public string Unit { get; init; } = string.Empty;
        /// <summary>
        /// Gets the status.
        /// </summary>
        public string Status { get; init; } = string.Empty;
        /// <summary>
        /// Gets the note.
        /// </summary>
        public string Note { get; init; } = string.Empty;
    }
}
