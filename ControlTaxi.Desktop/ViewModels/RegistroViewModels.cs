using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class RegistroDiarioViewModel(IServiceScopeFactory scopeFactory, DesktopSession session) : ObservableObject
{
    private PosOperacionRowViewModel? _selectedOperacion;
    private string _message = string.Empty;
    public ObservableCollection<PosOperacionRowViewModel> Operaciones { get; } = [];
    public string FolioOperacion { get; set; } = string.Empty;
    public DateTime? FechaInicio { get; set; } = DateTime.Today;
    public DateTime? FechaFin { get; set; } = DateTime.Today;
    public DateTime FechaTrabajo { get; set; } = DateTime.Today;
    public string Staff { get; set; } = string.Empty;
    public int Pax { get; set; } = 1;
    public decimal TotalEfectivo { get; private set; }
    public decimal TotalTarjeta { get; private set; }
    public decimal TotalGeneral { get; private set; }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public AsyncRelayCommand ConsultarCommand { get; } = new(async () => await Task.CompletedTask);
    public AsyncRelayCommand GuardarCommand { get; } = new(async () => await Task.CompletedTask);

    public PosOperacionRowViewModel? SelectedOperacion
    {
        get => _selectedOperacion;
        set
        {
            if (SetProperty(ref _selectedOperacion, value) && value is not null)
            {
                FolioOperacion = value.FolioOperacion;
                Staff = value.Vendedor;
                Pax = value.Pax;
                OnPropertyChanged(nameof(FolioOperacion));
                OnPropertyChanged(nameof(Staff));
                OnPropertyChanged(nameof(Pax));
            }
        }
    }

    public async Task ConsultarAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosRegistroService>();
        var model = await service.TryGetAsync(FolioOperacion, FechaInicio, FechaFin) ?? new PosRegistroDiarioViewModel();
        Operaciones.Clear();
        foreach (var row in model.Operaciones)
            Operaciones.Add(row);
        TotalEfectivo = model.TotalEfectivo;
        TotalTarjeta = model.TotalTarjeta;
        TotalGeneral = model.TotalGeneral;
        OnPropertyChanged(nameof(TotalEfectivo)); OnPropertyChanged(nameof(TotalTarjeta)); OnPropertyChanged(nameof(TotalGeneral));
        Message = "Registro diario consultado.";
    }

    public async Task GuardarAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosRegistroService>();
        var ok = await service.UpdateAsync(FolioOperacion, Staff, Pax, FechaTrabajo, session.Usuario);
        Message = ok ? "Registro actualizado." : "No se pudo actualizar registro.";
        if (ok) await ConsultarAsync();
    }
}

public sealed class RegistroAppDesktopViewModel(IServiceScopeFactory scopeFactory, DesktopSession session) : ObservableObject
{
    private string _message = string.Empty;
    public ObservableCollection<PosRegistroAppTaxistaOption> Taxistas { get; } = [];
    public ObservableCollection<PosRegistroAppTarifaOption> Tarifas { get; } = [];
    public ObservableCollection<string> Hoteles { get; } = [];
    public string Busqueda { get; set; } = string.Empty;
    public PosRegistroAppViewModel Model { get; } = new();
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public async Task BuscarCatalogosAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosRegistroService>();
        Replace(Taxistas, await service.SearchTaxistasAsync(Busqueda));
        Replace(Tarifas, await service.SearchTarifasAsync(Busqueda));
        Replace(Hoteles, await service.SearchHotelesAsync(Busqueda));
        Message = "Catalogos de Registro App actualizados.";
    }

    public async Task CrearAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosRegistroService>();
        var folio = await service.CreateAppAsync(Model, session.Usuario);
        Message = string.IsNullOrWhiteSpace(folio) ? "No se pudo crear registro App." : $"Registro App creado: {folio}.";
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> rows)
    {
        target.Clear();
        foreach (var row in rows) target.Add(row);
    }
}
