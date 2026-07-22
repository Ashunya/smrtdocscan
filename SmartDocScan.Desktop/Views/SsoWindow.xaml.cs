using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using SmartDocScan.Desktop.Services;
using Wpf.Ui.Controls;

namespace SmartDocScan.Desktop.Views;

public partial class SsoWindow : FluentWindow
{
    private readonly ApiClient _apiClient;
    private readonly CookieContainer _cookieContainer;
    private bool _authenticated = false;
    private bool _callbackReached = false;

    [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool InternetGetCookieEx(
        string url,
        string? cookieName,
        StringBuilder cookieData,
        ref int size,
        int flags,
        IntPtr reserved);

    private const int INTERNET_COOKIE_HTTPONLY = 0x2000;

    public SsoWindow(ApiClient apiClient, CookieContainer cookieContainer)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _cookieContainer = cookieContainer;

        Loaded += (s, e) =>
        {
            try
            {
                var baseUrl = _apiClient.BaseUrl.TrimEnd('/');
                var ssoUrl = $"{baseUrl}/api/auth/microsoft?returnUrl=/";
                Browser.Navigate(new Uri(ssoUrl));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to launch SSO browser: {ex.Message}", "SSO Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                DialogResult = false;
            }
        };
    }

    private void OnBrowserLoadCompleted(object sender, NavigationEventArgs e)
    {
        if (_authenticated) return;

        // Auto-submit Microsoft's hidden OAuth callback form if script execution was restricted
        try
        {
            dynamic doc = Browser.Document;
            if (doc != null && doc.forms != null && doc.forms.Length > 0)
            {
                doc.forms[0].submit();
            }
        }
        catch
        {
            // Ignore DOM access errors if non-HTML or cross-domain
        }

        CheckAuthenticationStatus(e.Uri);
    }

    private void OnBrowserNavigated(object sender, NavigationEventArgs e)
    {
        CheckAuthenticationStatus(e.Uri);
    }

    private void CheckAuthenticationStatus(Uri? uri)
    {
        if (uri == null || _authenticated) return;

        var urlString = uri.ToString();

        // 1. Mark when the Microsoft OAuth callback endpoint is reached
        if (urlString.Contains("/api/auth/microsoft/callback"))
        {
            _callbackReached = true;
        }

        // 2. Only complete authentication AFTER we have passed through callback or landed on post-login redirect
        if (_callbackReached && !urlString.Contains("login.microsoftonline.com") && !urlString.Contains("/api/auth/microsoft/callback"))
        {
            ExtractAndSaveCookies(_apiClient.BaseUrl);

            var cookies = _cookieContainer.GetCookies(new Uri(_apiClient.BaseUrl));
            var sessionCookie = cookies["smartdocscan.session"];

            if (sessionCookie != null && !string.IsNullOrWhiteSpace(sessionCookie.Value))
            {
                _authenticated = true;
                DialogResult = true;
                Close();
            }
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
}
