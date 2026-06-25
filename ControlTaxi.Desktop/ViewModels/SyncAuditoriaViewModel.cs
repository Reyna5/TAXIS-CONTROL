using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Data;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class SyncAuditoriaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private string _message = string.Empty;

    public SyncAuditoriaViewModel(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        FechaInicio = DateTime.Today;
        FechaFin = DateTime.Today;
        ProbarApiCommand = new AsyncRelayCommand(ProbarApiAsync);
        CargarAuditoriaCommand = new AsyncRelayCommand(CargarAuditoriaAsync);
        AbrirConfiguracionCommand = new RelayCommand(() => Message = $"Edite configuracion local en: {DesktopSettingsPaths.EnsureLocalSettingsFile()}");
        AutoSyncEnabled = _configuration.GetValue<bool>("PosSqlMirror:AutoSyncAppMovilFromApi");
        ApiBaseUrl = _configuration["TaxiApi:BaseUrl"] ?? string.Empty;
        _ = CargarAuditoriaAsync();
    }

    public ObservableCollection<PosOperacionRowViewModel> RegistrosApi { get; } = [];
    public ObservableCollection<PosRegistroAppTarifaOption> TarifasApi { get; } = [];
    public ObservableCollection<string> HotelesApi { get; } = [];
    public ObservableCollection<PosAuditoriaMovimiento> Auditoria { get; } = [];

    public AsyncRelayCommand ProbarApiCommand { get; }
    public AsyncRelayCommand CargarAuditoriaCommand { get; }
    public RelayCommand AbrirConfiguracionCommand { get; }

    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string FiltroAuditoria { get; set; } = string.Empty;
    public bool AutoSyncEnabled { get; }
    public string ApiBaseUrl { get; }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    private async Task ProbarApiAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IAppTaxiApiClient>();
        Replace(RegistrosApi, await api.GetTripRecordsAsync(null, FechaInicio, FechaFin));
        Replace(TarifasApi, await api.GetCatalogRatesAsync());
        Replace(HotelesApi, await api.GetCatalogHotelsAsync());
        Message = $"API consultada. Registros: {RegistrosApi.Count}, tarifas: {TarifasApi.Count}, hoteles: {HotelesApi.Count}.";
    }

    private async Task CargarAuditoriaAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var query = db.AuditoriaMovimientos.AsNoTracking();
        if (FechaInicio.HasValue)
            query = query.Where(x => x.FechaUtc >= FechaInicio.Value.Date);
        if (FechaFin.HasValue)
            query = query.Where(x => x.FechaUtc < FechaFin.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(FiltroAuditoria))
        {
            var filtro = FiltroAuditoria.Trim();
            query = query.Where(x =>
                x.Usuario.Contains(filtro) ||
                x.Modulo.Contains(filtro) ||
                x.Accion.Contains(filtro) ||
                x.IdRegistro.Contains(filtro) ||
                x.Descripcion.Contains(filtro));
        }
        var rows = await query.OrderByDescending(x => x.FechaUtc).Take(300).ToListAsync();
        Replace(Auditoria, rows);
        Message = $"{Auditoria.Count} movimientos de auditoria cargados.";
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }
}
