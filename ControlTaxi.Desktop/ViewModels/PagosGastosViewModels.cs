using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class PagosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private PosPagosViewModel _model = new();
    private string _message = string.Empty;

    public PagosViewModel(IServiceScopeFactory scopeFactory, DesktopSession session)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        RegistrarCommand = new AsyncRelayCommand(RegistrarAsync);
        _ = ConsultarAsync();
    }

    public PosPagosViewModel Model { get => _model; private set => SetProperty(ref _model, value); }
    public string FolioOperacion { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand RegistrarCommand { get; }

    public async Task ConsultarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosPagosService>();
        Model = await service.TryGetAsync(FolioOperacion) ?? new PosPagosViewModel { FolioOperacion = FolioOperacion };
        Importe = Model.ImporteCaptura;
        OnPropertyChanged(nameof(Importe));
        Message = "Pagos consultados.";
    }

    public async Task RegistrarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosPagosService>();
        var ok = await service.RegisterAsync(FolioOperacion, Importe, _session.Usuario);
        Message = ok ? "Pago registrado." : "No se pudo registrar pago.";
        if (ok)
            await ConsultarAsync();
    }
}

public sealed class GastosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private PosGastosViewModel _model = new();
    private string _message = string.Empty;

    public GastosViewModel(IServiceScopeFactory scopeFactory, DesktopSession session)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
        _ = ConsultarAsync();
    }

    public PosGastosViewModel Model { get => _model; private set => SetProperty(ref _model, value); }
    public ObservableCollection<GastoRow> Filas { get; } = [];
    public string FolioOperacion { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand GuardarCommand { get; }

    public async Task ConsultarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosGastosService>();
        Model = await service.TryGetAsync(FolioOperacion) ?? new PosGastosViewModel { FolioOperacion = FolioOperacion };
        Filas.Clear();
        foreach (var row in Model.Filas)
            Filas.Add(GastoRow.From(row));
        Importe = Model.ImporteCaptura;
        OnPropertyChanged(nameof(Importe));
        Message = "Gastos consultados.";
    }

    public async Task GuardarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosGastosService>();
        var ok = await service.UpdateAsync(FolioOperacion, Importe, Concepto, Observaciones, _session.Usuario);
        Message = ok ? "Gasto guardado." : "No se pudo guardar gasto.";
        if (ok)
            await ConsultarAsync();
    }
}

public sealed record GastoRow(string Fecha, string Concepto, string Importe, string Observacion, string Estatus)
{
    public static GastoRow From(IReadOnlyList<string> row) => new(
        row.Count > 0 ? row[0] : string.Empty,
        row.Count > 1 ? row[1] : string.Empty,
        row.Count > 2 ? row[2] : string.Empty,
        row.Count > 3 ? row[3] : string.Empty,
        row.Count > 4 ? row[4] : string.Empty);
}
