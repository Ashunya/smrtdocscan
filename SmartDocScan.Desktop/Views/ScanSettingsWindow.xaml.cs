using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SmartDocScan.Desktop.Models;
using Wpf.Ui.Controls;

namespace SmartDocScan.Desktop.Views;

public partial class ScanSettingsWindow : FluentWindow
{
    public string SelectedScanner { get; private set; } = "";
    public int SelectedDpi { get; private set; } = 300;
    public LocationModel? SelectedLocation { get; private set; }

    public ScanSettingsWindow(List<string> availableScanners, List<LocationModel> locations, LocationModel? defaultLocation = null)
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

        foreach (var loc in locations)
        {
            LocationComboBox.Items.Add(loc);
        }

        if (defaultLocation != null)
        {
            LocationComboBox.SelectedItem = locations.FirstOrDefault(l => l.LocationId == defaultLocation.LocationId) ?? LocationComboBox.Items.Cast<LocationModel>().FirstOrDefault();
        }
        else if (LocationComboBox.Items.Count > 0)
        {
            LocationComboBox.SelectedIndex = 0;
        }
    }

    private void OnStartScanClicked(object sender, RoutedEventArgs e)
    {
        SelectedScanner = ScannerComboBox.SelectedItem?.ToString() ?? "";
        SelectedLocation = LocationComboBox.SelectedItem as LocationModel;
        
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
