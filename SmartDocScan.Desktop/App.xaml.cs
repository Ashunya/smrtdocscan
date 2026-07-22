using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartDocScan.Desktop.Services;
using SmartDocScan.Desktop.ViewModels;

namespace SmartDocScan.Desktop;

public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<System.Net.CookieContainer>();
            services.AddHttpClient<ApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(sp => new System.Net.Http.HttpClientHandler
                    {
                        CookieContainer = sp.GetRequiredService<System.Net.CookieContainer>(),
                        UseCookies = true,
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                    });
            services.AddSingleton<IScannerService, ScannerService>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<MainWindow>();
            services.AddTransient<Views.LoginWindow>();
        })
        .Build();

    public static T GetService<T>()
        where T : class
    {
        return _host.Services.GetRequiredService<T>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        SetWebBrowserEmulation();

        this.DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show($"Application Error:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", 
                            "SmartDocScan Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Fatal Error:\n\n{ex.Message}\n\n{ex.StackTrace}", 
                                "SmartDocScan Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        _host.Start();

        var loginWindow = GetService<Views.LoginWindow>();
        loginWindow.Show();

        base.OnStartup(e);
    }

    private static void SetWebBrowserEmulation()
    {
        try
        {
            var appName = System.IO.Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName);
            if (string.IsNullOrEmpty(appName)) return;

            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION",
                Microsoft.Win32.RegistryKeyPermissionCheck.ReadWriteSubTree);

            key?.SetValue(appName, 11001, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync().Wait();
        _host.Dispose();

        base.OnExit(e);
    }
}
