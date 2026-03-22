using System.Windows;

namespace Ct3xxSimulator.Desktop.Views;

public partial class InputDialog : Window
{
    public InputDialog(Window owner, string prompt, string? defaultValue = null)
    {
        InitializeComponent();
        Owner = owner;
        PromptText.Text = prompt;
        InputBox.Text = defaultValue ?? string.Empty;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string Response => InputBox.Text;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
