using System.Collections.Generic;
using System.Windows;
using Wpf.Ui.Controls;

namespace SmartDocScan.Desktop.Views;

public partial class ScanSettingsWindow : FluentWindow
{
    public string SelectedScanner { get; private set; } = "";
    public int SelectedDpi { get; private set; } = 300;

    public ScanSettingsWindow(List<string> availableScanners)
    {
        InitializeComponent();
        
        foreach (var scanner in availableScanners)
        {
            ScannerComboBox.Items.Add(scanner);
        }

        if (ScannerComboBox.Items.Count > 0)
        {
            ScannerComboBox.SelectedIndex = 0;
        }
    }

    private void OnStartScanClicked(object sender, RoutedEventArgs e)
    {
        SelectedScanner = ScannerComboBox.SelectedItem?.ToString() ?? "";
        
        var dpiText = (DpiComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
        if (dpiText?.Contains("150") == true) SelectedDpi = 150;
        else if (dpiText?.Contains("600") == true) SelectedDpi = 600;
        else SelectedDpi = 300;

        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
