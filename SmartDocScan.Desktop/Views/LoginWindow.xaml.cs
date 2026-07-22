using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SmartDocScan.Desktop.Services;
using Wpf.Ui.Controls;

namespace SmartDocScan.Desktop.Views;

public partial class LoginWindow : FluentWindow
{
    private readonly ApiClient _apiClient;
    private readonly CookieContainer _cookieContainer;

    [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool InternetGetCookieEx(
        string url,
        string? cookieName,
        StringBuilder cookieData,
        ref int size,
        int flags,
        IntPtr reserved);

    private const int INTERNET_COOKIE_HTTPONLY = 0x2000;

    public LoginWindow(ApiClient apiClient, CookieContainer cookieContainer)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _cookieContainer = cookieContainer;
        ServerUrlTextBox.Text = _apiClient.BaseUrl;
    }

    private async void OnSignInClicked(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Visibility = Visibility.Collapsed;
        
        if (!string.IsNullOrWhiteSpace(ServerUrlTextBox.Text))
        {
            _apiClient.BaseUrl = ServerUrlTextBox.Text;
        }

        var email = EmailTextBox.Text;
        var pass = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            ShowError("Please enter your email and password.");
            return;
        }

        SignInButton.IsEnabled = false;
        LoadingRing.Visibility = Visibility.Visible;

        try
        {
            var success = await _apiClient.LoginAsync(email, pass);
            if (success)
            {
                OpenMainWindowAndClose();
            }
            else
            {
                ShowError("Invalid email or password.");
            }
        }
        catch (System.Exception ex)
        {
            ShowError($"Connection error: {ex.Message}");
        }
        finally
        {
            SignInButton.IsEnabled = true;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnSsoClicked(object sender, RoutedEventArgs e)
    {
        ErrorTextBlock.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrWhiteSpace(ServerUrlTextBox.Text))
        {
            _apiClient.BaseUrl = ServerUrlTextBox.Text;
        }

        SignInButton.IsEnabled = false;
        LoadingRing.Visibility = Visibility.Visible;

        HttpListener? listener = null;
        try
        {
            // Pick a local loopback port
            int port = 5005;
            string loopbackUri = $"http://127.0.0.1:{port}/callback/";

            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(loopbackUri);
                listener.Start();
            }
            catch
            {
                port = 5006;
                loopbackUri = $"http://127.0.0.1:{port}/callback/";
                listener = new HttpListener();
                listener.Prefixes.Add(loopbackUri);
                listener.Start();
            }

            var baseUrl = _apiClient.BaseUrl.TrimEnd('/');
            var desktopCallback = $"/api/auth/desktop-callback?redirectUri={Uri.EscapeDataString(loopbackUri)}";
            var ssoUrl = $"{baseUrl}/api/auth/microsoft?returnUrl={Uri.EscapeDataString(desktopCallback)}";

            // Launch System Browser (Edge / Chrome)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ssoUrl,
                UseShellExecute = true
            });

            // Wait for HttpListener callback (timeout after 2 minutes)
            var getContextTask = listener.GetContextAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));

            var completed = await Task.WhenAny(getContextTask, timeoutTask);
            if (completed == getContextTask)
            {
                var context = await getContextTask;
                var sessionValue = context.Request.QueryString["session"];

                // Respond to browser tab
                string responseHtml = "<html><body style='font-family:Segoe UI,sans-serif;text-align:center;padding-top:60px;'><h2>&#10004; Authentication Successful!</h2><p>You may now close this browser tab and return to SmartDocScan Desktop.</p><script>window.close();</script></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();

                if (!string.IsNullOrWhiteSpace(sessionValue))
                {
                    var baseUri = new Uri(_apiClient.BaseUrl);
                    _cookieContainer.SetCookies(baseUri, $"smartdocscan.session={sessionValue}");
                    OpenMainWindowAndClose();
                    return;
                }
            }

            ShowError("Microsoft Single Sign-On timed out or was cancelled.");
        }
        catch (System.Exception ex)
        {
            ShowError($"SSO Error: {ex.Message}");
        }
        finally
        {
            try { listener?.Stop(); } catch { }
            SignInButton.IsEnabled = true;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void ExtractAndSaveCookies(string url)
    {
        var cookieHeader = GetWinInetCookie(url);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            var baseUri = new Uri(url);
            var parts = cookieHeader.Split(';');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    try
                    {
                        _cookieContainer.SetCookies(baseUri, trimmed);
                    }
                    catch
                    {
                        // Ignore individual cookie format warnings
                    }
                }
            }
        }
    }

    private static string? GetWinInetCookie(string url)
    {
        int size = 2048;
        var sb = new StringBuilder(size);
        if (!InternetGetCookieEx(url, null, sb, ref size, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero))
        {
            if (size <= 0) return null;
            sb = new StringBuilder(size);
            if (!InternetGetCookieEx(url, null, sb, ref size, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero))
            {
                return null;
            }
        }
        return sb.ToString();
    }

    private void OpenMainWindowAndClose()
    {
        try
        {
            var mainWindow = App.GetService<MainWindow>();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            this.Close();
        }
        catch (System.Exception ex)
        {
            ShowError($"Error opening main window: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
