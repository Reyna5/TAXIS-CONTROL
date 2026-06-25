using System.Windows;
using System.Windows.Controls;
using ControlTaxi.Desktop.ViewModels;

namespace ControlTaxi.Desktop.Views;

public partial class RegistroDiarioView : UserControl
{
    public RegistroDiarioView()
    {
        InitializeComponent();
    }

    private async void Consultar_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is RegistroDiarioViewModel model)
            await model.ConsultarAsync();
    }

    private async void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is RegistroDiarioViewModel model)
            await model.GuardarAsync();
    }
}
