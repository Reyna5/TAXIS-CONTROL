using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class CatalogosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private string _message = string.Empty;

    public CatalogosViewModel(IServiceScopeFactory scopeFactory, DesktopSession session)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        GuardarTransporteCommand = new AsyncRelayCommand(GuardarTransporteAsync);
        GuardarGuiaCommand = new AsyncRelayCommand(GuardarGuiaAsync);
        GuardarTaxistaCommand = new AsyncRelayCommand(GuardarTaxistaAsync);
        _ = ConsultarAsync();
    }

    public ObservableCollection<CatalogoRow> Transportes { get; } = [];
    public ObservableCollection<CatalogoRow> Guias { get; } = [];
    public ObservableCollection<CatalogoRow> Taxistas { get; } = [];

    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand GuardarTransporteCommand { get; }
    public AsyncRelayCommand GuardarGuiaCommand { get; }
    public AsyncRelayCommand GuardarTaxistaCommand { get; }

    public string BusquedaTaxista { get; set; } = string.Empty;

    public string TransporteClave { get; set; } = string.Empty;
    public string TransporteNombre { get; set; } = string.Empty;
    public decimal TransporteMinimo { get; set; }
    public decimal TransporteMaximo { get; set; }
    public decimal TransporteComision { get; set; }
    public decimal TransporteDescEfectivo { get; set; }
    public decimal TransporteDescTarjeta { get; set; }
    public decimal TransporteDescAmex { get; set; }

    public string GuiaClave { get; set; } = string.Empty;
    public string GuiaNombre { get; set; } = string.Empty;
    public string GuiaTelefono { get; set; } = string.Empty;
    public decimal GuiaComision { get; set; }
    public string GuiaEstatus { get; set; } = "Activo";

    public string TaxistaClave { get; set; } = string.Empty;
    public string TaxistaNombre { get; set; } = string.Empty;
    public string TaxistaTelefono { get; set; } = string.Empty;
    public string TaxistaUnidad { get; set; } = string.Empty;
    public string TaxistaPlacas { get; set; } = string.Empty;
    public string TaxistaEstatus { get; set; } = "Activo";

    public CatalogoRow? SelectedTransporte { get; set; }
    public CatalogoRow? SelectedGuia { get; set; }
    public CatalogoRow? SelectedTaxista { get; set; }

    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    private async Task ConsultarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosCatalogosService>();
        var transportes = await service.TryGetTransportesAsync();
        var guias = await service.TryGetGuiasAsync();
        var taxistas = await service.TryGetTaxistasAsync(BusquedaTaxista);
        Replace(Transportes, transportes?.Filas.Select(CatalogoRow.From) ?? []);
        Replace(Guias, guias?.Filas.Select(CatalogoRow.From) ?? []);
        Replace(Taxistas, taxistas?.Filas.Select(CatalogoRow.From) ?? []);
        Message = "Catalogos actualizados.";
    }

    private async Task GuardarTransporteAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosCatalogosService>();
        var ok = await service.SaveTransporteAsync(TransporteClave, TransporteNombre, TransporteMinimo, TransporteMaximo, TransporteComision, TransporteDescEfectivo, TransporteDescTarjeta, TransporteDescAmex, _session.Usuario);
        Message = ok ? "Transporte guardado." : "No se pudo guardar transporte.";
        await ConsultarAsync();
    }

    private async Task GuardarGuiaAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosCatalogosService>();
        var ok = await service.SaveGuiaAsync(GuiaClave, GuiaNombre, GuiaTelefono, GuiaComision, GuiaEstatus, _session.Usuario);
        Message = ok ? "Guia guardada." : "No se pudo guardar guia.";
        await ConsultarAsync();
    }

    private async Task GuardarTaxistaAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosCatalogosService>();
        var ok = await service.SaveTaxistaAsync(TaxistaClave, TaxistaNombre, TaxistaTelefono, TaxistaUnidad, TaxistaPlacas, TaxistaEstatus, _session.Usuario);
        Message = ok ? "Taxista guardado." : "No se pudo guardar taxista.";
        await ConsultarAsync();
    }

    public void ApplySelectedTransporte()
    {
        if (SelectedTransporte is null)
            return;
        TransporteClave = SelectedTransporte.C1;
        TransporteNombre = SelectedTransporte.C2;
        TransporteMinimo = SelectedTransporte.Decimal(3);
        TransporteMaximo = SelectedTransporte.Decimal(4);
        TransporteComision = SelectedTransporte.Decimal(5);
        NotifyTransportes();
    }

    public void ApplySelectedGuia()
    {
        if (SelectedGuia is null)
            return;
        GuiaClave = SelectedGuia.C1;
        GuiaNombre = SelectedGuia.C2;
        GuiaTelefono = SelectedGuia.C3;
        GuiaComision = SelectedGuia.Decimal(4);
        GuiaEstatus = string.IsNullOrWhiteSpace(SelectedGuia.C5) ? "Activo" : SelectedGuia.C5;
        NotifyGuias();
    }

    public void ApplySelectedTaxista()
    {
        if (SelectedTaxista is null)
            return;
        TaxistaClave = SelectedTaxista.C1;
        TaxistaNombre = SelectedTaxista.C2;
        TaxistaTelefono = SelectedTaxista.C3;
        TaxistaUnidad = SelectedTaxista.C4;
        TaxistaPlacas = SelectedTaxista.C5;
        TaxistaEstatus = string.IsNullOrWhiteSpace(SelectedTaxista.C6) ? "Activo" : SelectedTaxista.C6;
        NotifyTaxistas();
    }

    private void NotifyTransportes()
    {
        OnPropertyChanged(nameof(TransporteClave)); OnPropertyChanged(nameof(TransporteNombre)); OnPropertyChanged(nameof(TransporteMinimo));
        OnPropertyChanged(nameof(TransporteMaximo)); OnPropertyChanged(nameof(TransporteComision)); OnPropertyChanged(nameof(TransporteDescEfectivo));
        OnPropertyChanged(nameof(TransporteDescTarjeta)); OnPropertyChanged(nameof(TransporteDescAmex));
    }

    private void NotifyGuias()
    {
        OnPropertyChanged(nameof(GuiaClave)); OnPropertyChanged(nameof(GuiaNombre)); OnPropertyChanged(nameof(GuiaTelefono));
        OnPropertyChanged(nameof(GuiaComision)); OnPropertyChanged(nameof(GuiaEstatus));
    }

    private void NotifyTaxistas()
    {
        OnPropertyChanged(nameof(TaxistaClave)); OnPropertyChanged(nameof(TaxistaNombre)); OnPropertyChanged(nameof(TaxistaTelefono));
        OnPropertyChanged(nameof(TaxistaUnidad)); OnPropertyChanged(nameof(TaxistaPlacas)); OnPropertyChanged(nameof(TaxistaEstatus));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> rows)
    {
        target.Clear();
        foreach (var row in rows)
            target.Add(row);
    }
}

public sealed record CatalogoRow(string C1, string C2, string C3, string C4, string C5, string C6, string C7, string C8)
{
    public static CatalogoRow From(IReadOnlyList<string> row) => new(
        row.Count > 0 ? row[0] : string.Empty,
        row.Count > 1 ? row[1] : string.Empty,
        row.Count > 2 ? row[2] : string.Empty,
        row.Count > 3 ? row[3] : string.Empty,
        row.Count > 4 ? row[4] : string.Empty,
        row.Count > 5 ? row[5] : string.Empty,
        row.Count > 6 ? row[6] : string.Empty,
        row.Count > 7 ? row[7] : string.Empty);

    public decimal Decimal(int oneBasedIndex)
    {
        var value = oneBasedIndex switch
        {
            1 => C1,
            2 => C2,
            3 => C3,
            4 => C4,
            5 => C5,
            6 => C6,
            7 => C7,
            8 => C8,
            _ => string.Empty
        };
        return decimal.TryParse(value, out var parsed) ? parsed : 0m;
    }
}
