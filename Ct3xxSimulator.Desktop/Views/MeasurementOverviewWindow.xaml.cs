// Provides Measurement Overview Window for ICT/CTCT/SHRT results.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ct3xxSimulator.Desktop.ViewModels;

namespace Ct3xxSimulator.Desktop.Views;

public partial class MeasurementOverviewWindow : Window
{
    private readonly List<MeasurementEntryViewModel> _allEntries;
    private readonly string? _forcedTestType;
    private readonly string? _focusStepName;
    private bool _suppressFilterChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeasurementOverviewWindow"/> class.
    /// </summary>
    public MeasurementOverviewWindow(IReadOnlyList<MeasurementEntryViewModel> entries, string? forcedTestType = null, string? focusStepName = null)
    {
        _allEntries = entries?.ToList() ?? new List<MeasurementEntryViewModel>();
        _forcedTestType = string.IsNullOrWhiteSpace(forcedTestType) ? null : forcedTestType.Trim().ToUpperInvariant();
        _focusStepName = focusStepName;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyForcedFilter();
            ApplyFilter();
        };
    }

    /// <summary>
    /// Executes OnFilterChanged.
    /// </summary>
    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressFilterChanged)
        {
            return;
        }

        ApplyFilter();
    }

    /// <summary>
    /// Executes ApplyForcedFilter.
    /// </summary>
    private void ApplyForcedFilter()
    {
        if (_forcedTestType == null)
        {
            return;
        }

        _suppressFilterChanged = true;
        IctCheckBox.IsChecked = string.Equals(_forcedTestType, "ICT", StringComparison.OrdinalIgnoreCase);
        CtctCheckBox.IsChecked = string.Equals(_forcedTestType, "CTCT", StringComparison.OrdinalIgnoreCase);
        ShrtCheckBox.IsChecked = string.Equals(_forcedTestType, "SHRT", StringComparison.OrdinalIgnoreCase);
        _suppressFilterChanged = false;
    }

    /// <summary>
    /// Executes ApplyFilter.
    /// </summary>
    private void ApplyFilter()
    {
        if (IctCheckBox == null || CtctCheckBox == null || ShrtCheckBox == null || MeasurementsDataGrid == null || SummaryTextBlock == null || StatusTextBlock == null)
        {
            return;
        }

        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (IctCheckBox.IsChecked == true)
        {
            enabled.Add("ICT");
        }
        if (CtctCheckBox.IsChecked == true)
        {
            enabled.Add("CTCT");
        }
        if (ShrtCheckBox.IsChecked == true)
        {
            enabled.Add("SHRT");
        }

        List<MeasurementEntryViewModel> filtered;
        try
        {
            filtered = _allEntries
                .Where(entry => entry != null && enabled.Contains(entry.TestType ?? string.Empty))
                .OrderBy(entry => entry.TestType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.StepName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            MeasurementsDataGrid.ItemsSource = Array.Empty<MeasurementEntryViewModel>();
            SummaryTextBlock.Text = "Eintraege: 0";
            StatusTextBlock.Text = "Messuebersicht konnte nicht gefiltert werden.";
            return;
        }

        MeasurementsDataGrid.ItemsSource = filtered;
        SummaryTextBlock.Text = $"Eintraege: {filtered.Count.ToString(CultureInfo.InvariantCulture)}";
        StatusTextBlock.Text = filtered.Count == 0
            ? "Keine Eintraege fuer die aktuelle Auswahl vorhanden."
            : "Doppelklick oeffnet die Verdrahtung fuer den ausgewaehlten Eintrag.";

        if (!string.IsNullOrWhiteSpace(_focusStepName) && filtered.Count > 0)
        {
            var focus = filtered.FirstOrDefault(entry =>
                string.Equals(entry.StepName, _focusStepName, StringComparison.OrdinalIgnoreCase));
            if (focus != null)
            {
                MeasurementsDataGrid.SelectedItem = focus;
                MeasurementsDataGrid.ScrollIntoView(focus);
            }
        }
    }

    /// <summary>
    /// Executes OnRowDoubleClick.
    /// </summary>
    private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (MeasurementsDataGrid.SelectedItem is not MeasurementEntryViewModel entry)
        {
            return;
        }

        if (!entry.HasTraces)
        {
            MessageBox.Show(this, "Keine Verdrahtung fuer diesen Eintrag verfuegbar.", "Verdrahtung nicht verfuegbar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var window = new ConnectionGraphWindow(
            entry.StepName,
            entry.Traces,
            entry.CurvePoints)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    /// <summary>
    /// Executes OnClose.
    /// </summary>
    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
