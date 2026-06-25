using System.Windows;
using System.Windows.Controls;
using ControlTaxi.Desktop.ViewModels;

namespace ControlTaxi.Desktop.Views;

public partial class RegistroAppView : UserControl
{
    public RegistroAppView()
    {
        InitializeComponent();
    }

    private async void Buscar_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is RegistroAppDesktopViewModel model)
            await model.BuscarCatalogosAsync();
    }

    private async void Crear_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is RegistroAppDesktopViewModel model)
            await model.CrearAsync();
    }
}
