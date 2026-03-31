// Provides Scenario Preset Upgrade Window for the desktop application window logic.
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Ct3xxSimulator.Desktop.ViewModels;

namespace Ct3xxSimulator.Desktop.Views;

public partial class ScenarioPresetUpgradeWindow : Window
{
    private readonly IReadOnlyList<BreakpointUpgradeItem> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioPresetUpgradeWindow"/> class.
    /// </summary>
    public ScenarioPresetUpgradeWindow(Window owner, string scenarioName, IReadOnlyList<BreakpointUpgradeItem> items)
    {
        InitializeComponent();
        Owner = owner;
        _items = items;
        PromptTextBlock.Text = $"Szenario '{scenarioName}' upgraden";
        MappingsGrid.ItemsSource = _items;
    }

    /// <summary>
    /// Gets the selected mappings.
    /// </summary>
    public IReadOnlyDictionary<string, string> SelectedMappings =>
        _items
            .Where(item => item.SelectedTarget != null)
            .ToDictionary(item => item.OriginalKey, item => item.SelectedTarget!.NodeKey);

    /// <summary>
    /// Executes OnApply.
    /// </summary>
    private void OnApply(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
