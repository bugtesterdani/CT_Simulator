using System.Collections.Generic;
using System.Windows;

namespace Ct3xxSimulator.Desktop.Views;

public partial class SelectionDialog : Window
{
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

    public string SelectedOption => OptionsList.SelectedItem as string ?? string.Empty;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (OptionsList.SelectedItem == null)
        {
            return;
        }

        DialogResult = true;
    }
}
