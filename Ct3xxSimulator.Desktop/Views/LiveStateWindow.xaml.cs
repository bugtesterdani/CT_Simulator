using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Ct3xxSimulator.Desktop.ViewModels;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop.Views;

public partial class LiveStateWindow : Window
{
    private readonly Dictionary<string, IReadOnlyList<MeasurementCurvePoint>> _history = new(StringComparer.OrdinalIgnoreCase);

    public LiveStateWindow()
    {
        InitializeComponent();
    }

    public void UpdateSnapshot(SimulationStateSnapshot snapshot, IReadOnlyDictionary<string, List<MeasurementCurvePoint>> signalHistory)
    {
        CurrentStepTextBlock.Text = $"Aktueller Schritt: {snapshot.CurrentStep ?? "-"}";
        CurrentTimeTextBlock.Text = $"Simulationszeit: {snapshot.CurrentTimeMs} ms";
        SignalsGrid.ItemsSource = snapshot.Signals
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SimulationStateItemViewModel(item.Key, item.Value))
            .ToList();
        MeasurementBusGrid.ItemsSource = snapshot.MeasurementBuses
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SimulationStateItemViewModel(item.Key, item.Value))
            .ToList();
        DeviceInputsGrid.ItemsSource = snapshot.ExternalDeviceState.Inputs
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SimulationStateItemViewModel(item.Key, item.Value))
            .ToList();
        DeviceOutputsGrid.ItemsSource = snapshot.ExternalDeviceState.Outputs
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SimulationStateItemViewModel(item.Key, item.Value))
            .ToList();
        DeviceInternalsGrid.ItemsSource = snapshot.ExternalDeviceState.InternalSignals
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SimulationStateItemViewModel(item.Key, item.Value))
            .ToList();
        RelayStateList.ItemsSource = snapshot.RelayStates.Count == 0
            ? new List<string> { "Keine Relaisinformationen verfuegbar." }
            : snapshot.RelayStates;
        ElementStateList.ItemsSource = snapshot.ElementStates.Count == 0
            ? new List<string> { "Keine Elementzustandsinformationen verfuegbar." }
            : snapshot.ElementStates;
        FaultList.ItemsSource = snapshot.ActiveFaults.Count == 0
            ? new List<string> { "Keine aktiven Faults." }
            : snapshot.ActiveFaults;

        _history.Clear();
        foreach (var item in signalHistory.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            _history[item.Key] = item.Value;
        }

        var selected = SignalHistorySelector.SelectedItem as string;
        SignalHistorySelector.ItemsSource = _history.Keys.ToList();
        if (!string.IsNullOrWhiteSpace(selected) && _history.ContainsKey(selected))
        {
            SignalHistorySelector.SelectedItem = selected;
        }
        else if (SignalHistorySelector.Items.Count > 0 && SignalHistorySelector.SelectedIndex < 0)
        {
            SignalHistorySelector.SelectedIndex = 0;
        }
        else
        {
            RenderHistory(null);
        }
    }

    private void OnSignalHistoryChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = SignalHistorySelector.SelectedItem as string;
        RenderHistory(selected);
    }

    private void RenderHistory(string? signalName)
    {
        HistoryCanvas.Children.Clear();
        if (string.IsNullOrWhiteSpace(signalName) || !_history.TryGetValue(signalName, out var points) || points.Count == 0)
        {
            HistoryCanvas.Children.Add(new TextBlock
            {
                Text = "Kein Verlauf fuer das ausgewaehlte Signal vorhanden.",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5E564A")),
                FontSize = 16
            });
            return;
        }

        var numericPoints = points.Where(point => point.Value.HasValue).ToList();
        if (numericPoints.Count == 0)
        {
            RenderHistory(null);
            return;
        }

        const double left = 48;
        const double top = 18;
        const double width = 900;
        const double height = 120;

        var minTime = numericPoints.Min(point => point.TimeMs);
        var maxTime = Math.Max(minTime + 1, numericPoints.Max(point => point.TimeMs));
        var minValue = numericPoints.Min(point => point.Value!.Value);
        var maxValue = Math.Max(minValue + 0.001, numericPoints.Max(point => point.Value!.Value));

        var axisBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF768191"));
        HistoryCanvas.Children.Add(new Line { X1 = left, Y1 = top + height, X2 = left + width, Y2 = top + height, Stroke = axisBrush, StrokeThickness = 1.4 });
        HistoryCanvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = top + height, Stroke = axisBrush, StrokeThickness = 1.4 });

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E6F95")),
            StrokeThickness = 2.5
        };

        foreach (var point in numericPoints)
        {
            var x = left + (point.TimeMs - minTime) / (double)(maxTime - minTime) * width;
            var y = top + height - ((point.Value!.Value - minValue) / (maxValue - minValue) * height);
            polyline.Points.Add(new System.Windows.Point(x, y));

            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E6F95")),
                ToolTip = $"{signalName}: {point.Value?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} @ {point.TimeMs} ms"
            };
            Canvas.SetLeft(dot, x - 4);
            Canvas.SetTop(dot, y - 4);
            HistoryCanvas.Children.Add(dot);
        }

        HistoryCanvas.Children.Add(polyline);
        AddHistoryLabel(left - 10, top - 4, $"{maxValue:0.###}");
        AddHistoryLabel(left - 10, top + height - 10, $"{minValue:0.###}");
        AddHistoryLabel(left, top + height + 6, $"{minTime} ms");
        AddHistoryLabel(left + width - 40, top + height + 6, $"{maxTime} ms");
    }

    private void AddHistoryLabel(double left, double top, string text)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5E564A")),
            FontSize = 12
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        HistoryCanvas.Children.Add(label);
    }
}
