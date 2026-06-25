using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxi.Desktop.Services;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class ReportesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopExportService _exportService;
    private DateTime? _fechaInicio = DateTime.Today;
    private DateTime? _fechaFin = DateTime.Today;
    private string _busqueda = string.Empty;
    private string _tipoReporte = "operaciones";
    private string _message = string.Empty;
    private bool _isBusy;

    public ReportesViewModel(IServiceScopeFactory scopeFactory, DesktopExportService exportService)
    {
        _scopeFactory = scopeFactory;
        _exportService = exportService;
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync);
        ExportTaxiExcelCommand = new AsyncRelayCommand(ExportTaxiExcelAsync);
        ExportDejadasExcelCommand = new AsyncRelayCommand(ExportDejadasExcelAsync);
        ExportConcentradoExcelCommand = new AsyncRelayCommand(ExportConcentradoExcelAsync);
        ExportCuadreExcelCommand = new AsyncRelayCommand(ExportCuadreExcelAsync);
        ExportPagosComisionesExcelCommand = new AsyncRelayCommand(ExportPagosComisionesExcelAsync);
        _ = ConsultarAsync();
    }

    public ObservableCollection<PosOperacionRowViewModel> Operaciones { get; } = [];
    public ObservableCollection<PosRelacionRowViewModel> Dejadas { get; } = [];
    public ObservableCollection<PosComisionRowViewModel> PagosComisiones { get; } = [];
    public ObservableCollection<CuadreCamionesSummary> Camiones { get; } = [];

    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }
    public AsyncRelayCommand ExportPdfCommand { get; }
    public AsyncRelayCommand ExportTaxiExcelCommand { get; }
    public AsyncRelayCommand ExportDejadasExcelCommand { get; }
    public AsyncRelayCommand ExportConcentradoExcelCommand { get; }
    public AsyncRelayCommand ExportCuadreExcelCommand { get; }
    public AsyncRelayCommand ExportPagosComisionesExcelCommand { get; }

    public DateTime? FechaInicio
    {
        get => _fechaInicio;
        set => SetProperty(ref _fechaInicio, value);
    }

    public DateTime? FechaFin
    {
        get => _fechaFin;
        set => SetProperty(ref _fechaFin, value);
    }

    public string Busqueda
    {
        get => _busqueda;
        set => SetProperty(ref _busqueda, value);
    }

    public string TipoReporte
    {
        get => _tipoReporte;
        set => SetProperty(ref _tipoReporte, value);
    }

    public int TotalMovimientos => TipoReporte == "pagos-comisiones" ? PagosComisiones.Count : Dejadas.Count;
    public int TotalPax => Dejadas.Sum(x => x.Pax);
    public decimal TotalDejadas => Dejadas.Sum(x => x.Dejada);
    public decimal TotalPagos => PagosComisiones.Sum(x => x.Pago);
    public decimal TotalComision => PagosComisiones.Sum(x => x.Importe);

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    private async Task ConsultarAsync()
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var registro = scope.ServiceProvider.GetRequiredService<IPosRegistroService>();
            var relaciones = scope.ServiceProvider.GetRequiredService<IPosRelacionesService>();
            var comisiones = scope.ServiceProvider.GetRequiredService<IPosComisionesService>();

            var registroModel = await registro.TryGetAsync(null, FechaInicio, FechaFin) ?? new PosRegistroDiarioViewModel();
            Replace(Operaciones, registroModel.Operaciones);
            Replace(Dejadas, await relaciones.GetReporteDejadasAsync(FechaInicio, FechaFin, Busqueda));
            Replace(Camiones, await relaciones.GetCamionesResumenAsync(FechaInicio, FechaFin));
            Replace(PagosComisiones, await comisiones.GetPagosReporteAsync(FechaInicio, FechaFin));
            NotifyTotals();
            Message = "Reportes actualizados.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible consultar reportes. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportCsvAsync()
    {
        var table = TipoReporte == "pagos-comisiones" ? BuildPagosTable() : BuildDejadasTable();
        await SaveExportAsync("CSV", "csv", _exportService.BuildCsv(table));
    }

    private async Task ExportPdfAsync()
    {
        var lines = new List<string>
        {
            TipoReporte == "pagos-comisiones" ? "** REPORTE DE PAGOS DE COMISIONES **" : "** REPORTE DE OPERACIONES TOTAL **",
            $"Fecha del Reporte: {FechaInicio:dd/MM/yyyy} al {FechaFin:dd/MM/yyyy}",
            string.Empty
        };
        var table = TipoReporte == "pagos-comisiones" ? BuildPagosTable() : BuildDejadasTable();
        lines.Add(string.Join(" | ", table.Headers));
        lines.AddRange(table.Rows.Select(row => string.Join(" | ", row)));
        await SaveExportAsync("PDF", "pdf", _exportService.BuildPdf(lines));
    }

    private async Task ExportTaxiExcelAsync() =>
        await SaveExportAsync("Excel taxis", "xlsx", _exportService.BuildExcelWorkbook(BuildOperacionesTable()));

    private async Task ExportDejadasExcelAsync() =>
        await SaveExportAsync("Excel dejadas", "xlsx", _exportService.BuildExcelWorkbook(BuildDejadasTable()));

    private async Task ExportConcentradoExcelAsync()
    {
        var hoteles = new ExportTable(
            "Hoteles",
            ["Hotel", "Registros", "Pax", "Dejada", "Comision", "Venta"],
            Dejadas
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Hotel) ? "SIN HOTEL" : x.Hotel)
                .Select(g => (IReadOnlyList<string>)[g.Key, g.Count().ToString(), g.Sum(x => x.Pax).ToString(), g.Sum(x => x.Dejada).ToString("0.00"), g.Sum(x => x.Comision).ToString("0.00"), g.Sum(x => x.Venta).ToString("0.00")])
                .ToList());
        await SaveExportAsync("Excel concentrado", "xlsx", _exportService.BuildExcelWorkbook(BuildDejadasTable(), hoteles));
    }

    private async Task ExportCuadreExcelAsync() =>
        await SaveExportAsync("Excel cuadre", "xlsx", _exportService.BuildExcelWorkbook(BuildDejadasTable(), BuildCamionesTable()));

    private async Task ExportPagosComisionesExcelAsync() =>
        await SaveExportAsync("Excel pagos comisiones", "xlsx", _exportService.BuildExcelWorkbook(BuildPagosTable()));

    private async Task SaveExportAsync(string title, string extension, byte[] content)
    {
        var dialog = new SaveFileDialog
        {
            Title = $"Guardar {title}",
            FileName = $"control_taxi_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}",
            Filter = extension switch
            {
                "csv" => "CSV (*.csv)|*.csv",
                "pdf" => "PDF (*.pdf)|*.pdf",
                _ => "Excel (*.xlsx)|*.xlsx"
            }
        };

        if (dialog.ShowDialog() != true)
            return;

        await _exportService.SaveAsync(dialog.FileName, content);
        Message = $"Archivo guardado: {dialog.FileName}";
    }

    private ExportTable BuildOperacionesTable() => new(
        "Reporte taxis",
        ["Folio", "Folio control", "Ticket", "Hotel", "Hora", "Pax", "Taxista", "Tipo", "Total"],
        Operaciones.Select(x => (IReadOnlyList<string>)[x.FolioOperacion, x.FolioControl, x.Ticket, x.Hotel, x.Hora, x.Pax.ToString(), x.Vendedor, x.TipoOperacion, x.Total.ToString("0.00")]).ToList());

    private ExportTable BuildDejadasTable() => new(
        "Control dejadas",
        ["Folio control", "Folio app", "Folio operacion", "Fecha", "Hotel", "Taxista", "Gafete", "Pax", "Dejada", "Comision", "Pago", "Estatus"],
        Dejadas.Select(x => (IReadOnlyList<string>)[x.FolioControl, x.FolioApp, x.FolioOperacion, x.Fecha, x.Hotel, x.TaxistaNombre, x.Gafete, x.Pax.ToString(), x.Dejada.ToString("0.00"), x.Comision.ToString("0.00"), x.Pago.ToString("0.00"), x.Estatus]).ToList());

    private ExportTable BuildPagosTable() => new(
        "Pagos comisiones",
        ["Folio", "Fecha", "Fecha pago", "Taxista", "Transporte", "Comision", "Pago", "Estatus"],
        PagosComisiones.Select(x => (IReadOnlyList<string>)[x.Folio, x.Fecha, x.FechaPago, string.IsNullOrWhiteSpace(x.Staff) ? x.Beneficiario : x.Staff, x.Transporte, x.Importe.ToString("0.00"), x.Pago.ToString("0.00"), x.Estatus]).ToList());

    private ExportTable BuildCamionesTable() => new(
        "Cuadre camiones",
        ["Nombre", "Pax", "Entraron", "Se fueron", "Unidades", "Dejada"],
        Camiones.Select(x => (IReadOnlyList<string>)[x.Nombre, x.Pax.ToString(), x.Entraron.ToString(), x.SeFueron.ToString(), x.Unidades.ToString(), x.Dejada.ToString("0.00")]).ToList());

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalMovimientos));
        OnPropertyChanged(nameof(TotalPax));
        OnPropertyChanged(nameof(TotalDejadas));
        OnPropertyChanged(nameof(TotalPagos));
        OnPropertyChanged(nameof(TotalComision));
    }
}
