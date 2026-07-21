using System.Windows;
using SmartDocScan.Desktop.Services;
using Wpf.Ui.Controls;

namespace SmartDocScan.Desktop.Views;

public partial class LoginWindow : FluentWindow
{
    private readonly ApiClient _apiClient;

    public LoginWindow(ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    private void OnSignInClicked(object sender, RoutedEventArgs e)
    {
        // Simple stub for now. In a real app, we'd authenticate against ApiClient
        // and grab a JWT token.
        
        var email = EmailTextBox.Text;
        var pass = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            System.Windows.MessageBox.Show("Please enter email and password", "Error", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Simulate success and open main window
        var mainWindow = App.GetService<MainWindow>();
        mainWindow.Show();
        
        this.Close();
    }
}
