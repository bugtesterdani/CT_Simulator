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
    private bool _updatingSelector;

    public LiveStateWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyViewMode();
    }

    public void UpdateSnapshot(SimulationStateSnapshot snapshot, IReadOnlyDictionary<string, List<MeasurementCurvePoint>> signalHistory)
    {
        CurrentStepTextBlock.Text = $"Aktueller Schritt: {snapshot.CurrentStep ?? "-"}";
        CurrentTimeTextBlock.Text = $"Simulationszeit: {snapshot.CurrentTimeMs} ms";
        ConcurrentGroupTextBlock.Text = $"Concurrent-Gruppe: {snapshot.ActiveConcurrentGroup ?? "-"}";
        ConcurrentEventTextBlock.Text = $"Concurrent-Event: {snapshot.ConcurrentEvent ?? "-"}";
        var signals = CreateStateItems(snapshot.Signals);
        var measurementBuses = CreateStateItems(snapshot.MeasurementBuses);
        var deviceInputs = CreateStateItems(snapshot.ExternalDeviceState.Inputs);
        var deviceSources = CreateStateItems(snapshot.ExternalDeviceState.Sources);
        var deviceOutputs = CreateStateItems(snapshot.ExternalDeviceState.Outputs);
        var deviceInternals = CreateStateItems(snapshot.ExternalDeviceState.InternalSignals);
        var deviceInterfaces = CreateStateItems(snapshot.ExternalDeviceState.Interfaces);
        var relayStates = snapshot.RelayStates.Count == 0
            ? new List<string> { "Keine Relaisinformationen verfuegbar." }
            : snapshot.RelayStates.ToList();
        var elementStates = snapshot.ElementStates.Count == 0
            ? new List<string> { "Keine Elementzustandsinformationen verfuegbar." }
            : snapshot.ElementStates.ToList();
        var faultStates = snapshot.ActiveFaults.Count == 0
            ? new List<string> { "Keine aktiven Faults." }
            : snapshot.ActiveFaults.ToList();
        var concurrentBranches = snapshot.ConcurrentBranches.Count == 0
            ? new List<ConcurrentBranchSnapshot> { new("Keine aktive Concurrent-Gruppe", null, "-", null, null) }
            : snapshot.ConcurrentBranches
                .OrderBy(item => item.BranchName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        SignalsGrid.ItemsSource = signals;
        MeasurementBusGrid.ItemsSource = measurementBuses;
        DeviceInputsGrid.ItemsSource = deviceInputs;
        DeviceSourcesGrid.ItemsSource = deviceSources;
        DeviceOutputsGrid.ItemsSource = deviceOutputs;
        DeviceInternalsGrid.ItemsSource = deviceInternals;
        DeviceInterfacesGrid.ItemsSource = deviceInterfaces;
        RelayStateList.ItemsSource = relayStates;
        ElementStateList.ItemsSource = elementStates;
        FaultList.ItemsSource = faultStates;
        ConcurrentBranchesGrid.ItemsSource = concurrentBranches;

        CompactSignalsGrid.ItemsSource = signals;
        CompactMeasurementBusGrid.ItemsSource = measurementBuses;
        CompactDeviceIoGrid.ItemsSource = deviceInputs
            .Select(item => new SectionStateItemViewModel("Input", item.Name, item.Value))
            .Concat(deviceOutputs.Select(item => new SectionStateItemViewModel("Output", item.Name, item.Value)))
            .ToList();
        CompactDeviceExtraGrid.ItemsSource = deviceSources
            .Select(item => new SectionStateItemViewModel("Source", item.Name, item.Value))
            .Concat(deviceInternals.Select(item => new SectionStateItemViewModel("Internal", item.Name, item.Value)))
            .Concat(deviceInterfaces.Select(item => new SectionStateItemViewModel("Interface", item.Name, item.Value)))
            .ToList();
        CompactStateList.ItemsSource = relayStates
            .Concat(elementStates)
            .ToList();
        CompactConcurrentBranchesGrid.ItemsSource = concurrentBranches;
        CompactFaultList.ItemsSource = faultStates;

        _history.Clear();
        foreach (var item in signalHistory.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            _history[item.Key] = item.Value;
        }

        var selected = SignalHistorySelector.SelectedItem as string;
        _updatingSelector = true;
        SignalHistorySelector.ItemsSource = _history.Keys.ToList();
        if (!string.IsNullOrWhiteSpace(selected) && _history.ContainsKey(selected))
        {
            SignalHistorySelector.SelectedItem = selected;
        }
        else if (SignalHistorySelector.Items.Count > 0)
        {
            SignalHistorySelector.SelectedIndex = 0;
        }
        else
        {
            SignalHistorySelector.SelectedIndex = -1;
        }
        _updatingSelector = false;

        RenderHistory(SignalHistorySelector.SelectedItem as string);
    }

    private static List<SimulationStateItemViewModel> CreateStateItems(IReadOnlyDictionary<string, string> items)
    {
        return items
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new SimulationStateItemViewModel(item.Key, item.Value))
            .ToList();
    }

    private void OnViewModeChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyViewMode();
    }

    private void ApplyViewMode()
    {
        if (ViewModeSelector == null || CompactPanel == null || ExpertPanel == null)
        {
            return;
        }

        if (ViewModeSelector.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var isExpert = string.Equals(item.Tag as string, "expert", StringComparison.OrdinalIgnoreCase);
        CompactPanel.Visibility = isExpert ? Visibility.Collapsed : Visibility.Visible;
        ExpertPanel.Visibility = isExpert ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSignalHistoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelector)
        {
            return;
        }

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
        const double width = 1030;
        const double height = 145;

        HistoryCanvas.Width = width + 100;
        HistoryCanvas.Height = 210;

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
        AddHistoryLabel(left + width - 55, top + height + 6, $"{maxTime} ms");
        AddHistoryLabel(left + 6, top - 24, signalName);
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

    private sealed class SectionStateItemViewModel
    {
        public SectionStateItemViewModel(string section, string name, string value)
        {
            Section = section;
            Name = name;
            Value = value;
        }

        public string Section { get; }
        public string Name { get; }
        public string Value { get; }
    }
}
