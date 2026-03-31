// Provides Input Dialog for the desktop application window logic.
using System.Windows;

namespace Ct3xxSimulator.Desktop.Views;

public partial class InputDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InputDialog"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the response.
    /// </summary>
    public string Response => InputBox.Text;

    /// <summary>
    /// Executes OnOk.
    /// </summary>
    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
