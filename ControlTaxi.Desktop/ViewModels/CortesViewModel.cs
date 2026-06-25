using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxi.Desktop.Services;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class CortesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private readonly DesktopExportService _exportService;
    private readonly TicketPrintService _printService;
    private PosCorteViewModel _model = new();
    private string _message = string.Empty;
    private bool _isBusy;

    public CortesViewModel(IServiceScopeFactory scopeFactory, DesktopSession session, DesktopExportService exportService, TicketPrintService printService)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        _exportService = exportService;
        _printService = printService;
        Fecha = DateTime.Today;
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        CalcularCommand = new AsyncRelayCommand(CalcularAsync);
        CerrarCommand = new AsyncRelayCommand(CerrarAsync);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync);
        ImprimirCommand = new RelayCommand(Imprimir);
        _ = ConsultarAsync();
    }

    public ObservableCollection<CorteConceptoRow> Conceptos { get; } = [];

    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand CalcularCommand { get; }
    public AsyncRelayCommand CerrarCommand { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }
    public AsyncRelayCommand ExportPdfCommand { get; }
    public RelayCommand ImprimirCommand { get; }

    public DateTime Fecha { get; set; }
    public decimal TotalCobrado => Model.Efectivo + Model.Tarjeta + Model.Amex;
    public PosCorteViewModel Model { get => _model; private set => SetProperty(ref _model, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    private async Task ConsultarAsync()
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPosCortesService>();
            Model = await service.TryGetAsync(Fecha) ?? new PosCorteViewModel { Fecha = Fecha };
            Fecha = Model.Fecha;
            BuildRows();
            Message = "Corte consultado.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible consultar corte. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CalcularAsync()
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var comisiones = scope.ServiceProvider.GetRequiredService<IPosComisionesService>();
            var actualizados = await comisiones.RecalcularAsync(usuario: _session.Usuario, fecha: Fecha);
            Message = $"Corte recalculado. Comisiones actualizadas: {actualizados}.";
            await ConsultarAsync();
        }
        catch (Exception ex)
        {
            Message = "No fue posible calcular corte. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CerrarAsync()
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPosCortesService>();
            var ok = await service.CloseAsync(_session.Usuario, Fecha);
            Message = ok ? "Corte cerrado. El dia queda bloqueado para cambios normales." : "No se pudo cerrar el corte.";
            await ConsultarAsync();
        }
        catch (Exception ex)
        {
            Message = "No fue posible cerrar corte. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportCsvAsync() => await SaveAsync("CSV", "csv", _exportService.BuildCsv(BuildTable()));

    private async Task ExportPdfAsync() => await SaveAsync("PDF", "pdf", _exportService.BuildPdf(BuildPrintLines()));

    private void Imprimir() => _printService.PrintText(string.Join(Environment.NewLine, BuildPrintLines()));

    private async Task SaveAsync(string title, string extension, byte[] bytes)
    {
        var dialog = new SaveFileDialog
        {
            Title = $"Guardar corte {title}",
            FileName = $"corte_{Fecha:yyyyMMdd}.{extension}",
            Filter = extension == "csv" ? "CSV (*.csv)|*.csv" : "PDF (*.pdf)|*.pdf"
        };
        if (dialog.ShowDialog() != true)
            return;
        await _exportService.SaveAsync(dialog.FileName, bytes);
        Message = $"Archivo guardado: {dialog.FileName}";
    }

    private void BuildRows()
    {
        Conceptos.Clear();
        foreach (var row in BuildConceptRows())
            Conceptos.Add(row);
        OnPropertyChanged(nameof(Fecha));
        OnPropertyChanged(nameof(TotalCobrado));
    }

    private IReadOnlyList<CorteConceptoRow> BuildConceptRows() =>
    [
        new("EFECTIVO", Model.Efectivo),
        new("TARJETA", Model.Tarjeta),
        new("AMEX", Model.Amex),
        new("GASTOS", Model.Gastos),
        new("COMISIONES", Model.Comisiones),
        new("TOTAL DEL DIA", Model.TotalDia),
        new("TOTAL COBRADO", TotalCobrado),
        new("DIFERENCIA", Model.Diferencia)
    ];

    private ExportTable BuildTable() => new(
        "Corte",
        ["Concepto", "Importe"],
        BuildConceptRows().Select(x => (IReadOnlyList<string>)[x.Concepto, x.Importe.ToString("0.00")]).ToList());

    private IEnumerable<string> BuildPrintLines()
    {
        yield return "** REPORTE DE CIERRE / CORTE **";
        yield return $"Fecha del Reporte: {Fecha:dd/MM/yyyy} al {Fecha:dd/MM/yyyy}";
        yield return $"Movimientos: {Model.Movimientos}";
        yield return $"Estatus: {(Model.Cerrado ? "CERRADO" : "ABIERTO")}";
        yield return "--------------------------------";
        foreach (var row in BuildConceptRows())
            yield return $"{row.Concepto,-18} {row.Importe,12:0.00}";
    }
}

public sealed record CorteConceptoRow(string Concepto, decimal Importe);
