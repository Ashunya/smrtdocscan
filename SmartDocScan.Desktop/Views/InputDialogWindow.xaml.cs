using System.Windows;
using Wpf.Ui.Controls;

namespace SmartDocScan.Desktop.Views;

public partial class InputDialogWindow : FluentWindow
{
    public string InputText
    {
        get => InputTextBox.Text;
        set => InputTextBox.Text = value;
    }

    public InputDialogWindow(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptTextBlock.Text = prompt;
        InputTextBox.Text = defaultValue;
        InputTextBox.Focus();
        InputTextBox.SelectAll();
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static string? Prompt(string title, string prompt, string defaultValue = "", Window? owner = null)
    {
        var dialog = new InputDialogWindow(title, prompt, defaultValue);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        if (dialog.ShowDialog() == true)
        {
            return dialog.InputText;
        }
        return null;
    }
}
