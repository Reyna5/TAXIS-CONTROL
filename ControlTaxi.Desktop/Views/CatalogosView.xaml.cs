using System.Windows.Controls;
using ControlTaxi.Desktop.ViewModels;

namespace ControlTaxi.Desktop.Views;

public partial class CatalogosView : UserControl
{
    public CatalogosView()
    {
        InitializeComponent();
    }

    private void Transportes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CatalogosViewModel model)
            model.ApplySelectedTransporte();
    }

    private void Guias_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CatalogosViewModel model)
            model.ApplySelectedGuia();
    }

    private void Taxistas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is CatalogosViewModel model)
            model.ApplySelectedTaxista();
    }
}
