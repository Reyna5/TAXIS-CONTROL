using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class GafetesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private GafeteDesktopRow? _selectedRow;
    private string _message = string.Empty;
    private bool _isBusy;

    public GafetesViewModel(IServiceScopeFactory scopeFactory, DesktopSession session)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        FechaInicio = DateTime.Today;
        FechaFin = DateTime.Today;
        Movimiento = "A";
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        BuscarGafeteCommand = new AsyncRelayCommand(BuscarGafeteAsync);
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
        NuevoCommand = new RelayCommand(Nuevo);
        RegistrarRegresoCommand = new AsyncRelayCommand(RegistrarRegresoAsync);
        RegistrarRegresoSeleccionadosCommand = new AsyncRelayCommand(RegistrarRegresoSeleccionadosAsync);
        RegistrarRegresoBloqueCommand = new AsyncRelayCommand(RegistrarRegresoBloqueAsync);
        _ = ConsultarAsync();
    }

    public ObservableCollection<GafeteDesktopRow> Filas { get; } = [];

    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand BuscarGafeteCommand { get; }
    public AsyncRelayCommand GuardarCommand { get; }
    public RelayCommand NuevoCommand { get; }
    public AsyncRelayCommand RegistrarRegresoCommand { get; }
    public AsyncRelayCommand RegistrarRegresoSeleccionadosCommand { get; }
    public AsyncRelayCommand RegistrarRegresoBloqueCommand { get; }

    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public bool SoloSinFecha { get; set; }
    public int Matricula { get; set; }
    public string Gafete { get; set; } = string.Empty;
    public long? FolioOperacion { get; set; }
    public string Movimiento { get; set; }
    public bool EsEdicion { get; set; }
    public int? MatriculaOriginal { get; set; }
    public string GafeteOriginal { get; set; } = string.Empty;
    public long? FolioOperacionOriginal { get; set; }
    public string GafetesBloque { get; set; } = string.Empty;
    public int TotalFilas { get; private set; }

    public GafeteDesktopRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value) && value is not null)
                ApplyRow(value);
        }
    }

    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    private async Task ConsultarAsync()
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPosGafetesService>();
            var model = await service.TryGetAsync(FechaInicio, FechaFin, SoloSinFecha);
            Filas.Clear();
            foreach (var row in model?.Filas ?? [])
                Filas.Add(GafeteDesktopRow.From(row));
            TotalFilas = model?.TotalFilas ?? Filas.Count;
            OnPropertyChanged(nameof(TotalFilas));
            Message = $"{Filas.Count} gafetes cargados.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible consultar gafetes. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BuscarGafeteAsync()
    {
        if (string.IsNullOrWhiteSpace(Gafete))
        {
            Message = "Capture un gafete para buscar.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosGafetesService>();
        var row = await service.TryFindAsync(Gafete);
        if (row is null)
        {
            Message = "No se encontro movimiento activo para ese gafete.";
            return;
        }

        Matricula = int.TryParse(row.Staff, out var staff) ? staff : 0;
        FolioOperacion = long.TryParse(row.FolioOperacion, out var folio) ? folio : null;
        Movimiento = string.Equals(row.Estatus, "OCUPADO", StringComparison.OrdinalIgnoreCase) ? "A" : "R";
        NotifyForm();
        Message = $"Gafete {row.Numero} encontrado con estatus {row.Estatus}.";
    }

    private async Task GuardarAsync()
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPosGafetesService>();
            var ok = EsEdicion
                ? await service.UpdateAsync(MatriculaOriginal, GafeteOriginal, FolioOperacionOriginal, Matricula, Gafete, FolioOperacion, Movimiento, _session.Usuario)
                : await service.InsertAsync(Matricula, Gafete, FolioOperacion, Movimiento, _session.Usuario);
            Message = ok ? EsEdicion ? "Gafete actualizado." : "Gafete guardado." : "No se pudo guardar el gafete.";
            if (ok)
            {
                Nuevo();
                await ConsultarAsync();
            }
        }
        catch (Exception ex)
        {
            Message = "No fue posible guardar el gafete. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RegistrarRegresoAsync()
    {
        if (string.IsNullOrWhiteSpace(Gafete))
        {
            Message = "Gafete invalido.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosGafetesService>();
        var existing = await service.TryFindAsync(Gafete);
        if (existing is null)
        {
            Message = "No se encontro movimiento activo para ese gafete.";
            return;
        }

        var estatus = existing.Estatus.Trim();
        if (!string.Equals(estatus, "OCUPADO", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(estatus, "SUSPENDIDO", StringComparison.OrdinalIgnoreCase))
        {
            Message = $"Gafete {Gafete} no esta en uso; estatus actual: {estatus}.";
            return;
        }

        var ok = await service.MarcarRegresoAsync(Gafete, FolioOperacion, _session.Usuario);
        Message = ok ? $"Gafete {Gafete} marcado como regreso/libre." : "No se pudo actualizar el regreso.";
        if (ok)
            await ConsultarAsync();
    }

    private async Task RegistrarRegresoSeleccionadosAsync()
    {
        var selected = Filas.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0)
        {
            Message = "Seleccione al menos un gafete ocupado.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosGafetesService>();
        var actualizados = 0;
        var noActualizados = 0;
        foreach (var row in selected)
        {
            if (await service.MarcarRegresoAsync(row.Gafete, row.FolioOperacionNumero, _session.Usuario))
                actualizados++;
            else
                noActualizados++;
        }

        Message = $"Regreso masivo actualizado. Liberados: {actualizados}. Sin cambio: {noActualizados}.";
        await ConsultarAsync();
    }

    private async Task RegistrarRegresoBloqueAsync()
    {
        if (string.IsNullOrWhiteSpace(GafetesBloque))
        {
            Message = "Escanee o capture gafetes para regreso en bloque.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosGafetesService>();
        var result = await service.MarcarRegresoMasivoAsync(GafetesBloque, FolioOperacion, _session.Usuario);
        Message = $"Regreso en bloque actualizado. Liberados: {result.Actualizados}. Sin cambio: {result.NoActualizados}.";
        GafetesBloque = string.Empty;
        OnPropertyChanged(nameof(GafetesBloque));
        await ConsultarAsync();
    }

    private void ApplyRow(GafeteDesktopRow row)
    {
        EsEdicion = true;
        Matricula = int.TryParse(row.Staff, out var staff) ? staff : 0;
        Gafete = row.Gafete;
        FolioOperacion = row.FolioOperacionNumero;
        Movimiento = string.Equals(row.Estatus, "OCUPADO", StringComparison.OrdinalIgnoreCase) ? "A" : "R";
        MatriculaOriginal = Matricula;
        GafeteOriginal = Gafete;
        FolioOperacionOriginal = FolioOperacion;
        NotifyForm();
    }

    private void Nuevo()
    {
        EsEdicion = false;
        Matricula = 0;
        Gafete = string.Empty;
        FolioOperacion = null;
        Movimiento = "A";
        MatriculaOriginal = null;
        GafeteOriginal = string.Empty;
        FolioOperacionOriginal = null;
        NotifyForm();
    }

    private void NotifyForm()
    {
        OnPropertyChanged(nameof(EsEdicion));
        OnPropertyChanged(nameof(Matricula));
        OnPropertyChanged(nameof(Gafete));
        OnPropertyChanged(nameof(FolioOperacion));
        OnPropertyChanged(nameof(Movimiento));
        OnPropertyChanged(nameof(MatriculaOriginal));
        OnPropertyChanged(nameof(GafeteOriginal));
        OnPropertyChanged(nameof(FolioOperacionOriginal));
    }
}

public sealed class GafeteDesktopRow : ObservableObject
{
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string Gafete { get; init; } = string.Empty;
    public string Staff { get; init; } = string.Empty;
    public string FolioOperacion { get; init; } = string.Empty;
    public string Unidad { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public string Nacionalidad { get; init; } = string.Empty;
    public string Entrega { get; init; } = string.Empty;
    public string Estatus { get; init; } = string.Empty;
    public string Regreso { get; init; } = string.Empty;
    public long? FolioOperacionNumero => long.TryParse(FolioOperacion, out var folio) ? folio : null;

    public static GafeteDesktopRow From(IReadOnlyList<string> row) => new()
    {
        Gafete = row.Count > 0 ? row[0] : string.Empty,
        Staff = row.Count > 1 ? row[1] : string.Empty,
        FolioOperacion = row.Count > 2 ? row[2] : string.Empty,
        Unidad = row.Count > 3 ? row[3] : string.Empty,
        Telefono = row.Count > 4 ? row[4] : string.Empty,
        Nacionalidad = row.Count > 5 ? row[5] : string.Empty,
        Entrega = row.Count > 6 ? row[6] : string.Empty,
        Estatus = row.Count > 7 ? row[7] : string.Empty,
        Regreso = row.Count > 8 ? row[8] : string.Empty
    };
}
