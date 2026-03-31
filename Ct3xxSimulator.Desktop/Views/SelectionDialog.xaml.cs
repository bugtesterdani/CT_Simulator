// Provides Selection Dialog for the desktop application window logic.
using System.Collections.Generic;
using System.Windows;

namespace Ct3xxSimulator.Desktop.Views;

public partial class SelectionDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionDialog"/> class.
    /// </summary>
    public SelectionDialog(Window owner, string message, IReadOnlyList<string> options)
    {
        InitializeComponent();
        Owner = owner;
        PromptText.Text = message;
        OptionsList.ItemsSource = options;
        if (options.Count > 0)
        {
            OptionsList.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Gets the selected option.
    /// </summary>
    public string SelectedOption => OptionsList.SelectedItem as string ?? string.Empty;

    /// <summary>
    /// Executes OnOk.
    /// </summary>
    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (OptionsList.SelectedItem == null)
        {
            return;
        }

        DialogResult = true;
    }
}
