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
            services.AddHttpClient<ApiClient>();
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
        _host.Start();

        var loginWindow = GetService<Views.LoginWindow>();
        loginWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync().Wait();
        _host.Dispose();

        base.OnExit(e);
    }
}
