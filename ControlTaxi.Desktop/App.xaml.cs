using System.Windows;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxi.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlTaxi.Desktop;

public partial class App : Application
{
    private IHost? _host;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        _host = DesktopHost.Create();
        await _host.StartAsync();

        await _host.Services.GetRequiredService<DesktopSchemaInitializer>().TryInitializeAsync();

        var window = new MainWindow
        {
            DataContext = _host.Services.GetRequiredService<MainViewModel>()
        };
        window.Show();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        if (_host is null)
            return;

        await _host.StopAsync(TimeSpan.FromSeconds(3));
        _host.Dispose();
    }
}
