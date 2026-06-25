using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class ComisionesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private ComisionDesktopRow? _selectedRow;
    private string _message = string.Empty;
    private bool _isBusy;

    public ComisionesViewModel(IServiceScopeFactory scopeFactory, DesktopSession session)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        FechaInicio = DateTime.Today;
        FechaFin = DateTime.Today;
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        RecalcularCommand = new AsyncRelayCommand(RecalcularAsync);
        PagarSeleccionadaCommand = new AsyncRelayCommand(PagarSeleccionadaAsync);
        PagarSeleccionCommand = new AsyncRelayCommand(PagarSeleccionAsync);
        AbonarCommand = new AsyncRelayCommand(AbonarAsync);
        _ = ConsultarAsync(autoRecalcular: true);
    }

    public ObservableCollection<ComisionDesktopRow> Comisiones { get; } = [];

    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand RecalcularCommand { get; }
    public AsyncRelayCommand PagarSeleccionadaCommand { get; }
    public AsyncRelayCommand PagarSeleccionCommand { get; }
    public AsyncRelayCommand AbonarCommand { get; }

    public string FolioOperacion { get; set; } = string.Empty;
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public decimal ImporteAbono { get; set; }
    public decimal TotalVenta { get; private set; }
    public decimal TotalBase { get; private set; }
    public decimal TotalComision { get; private set; }
    public decimal TotalPagado { get; private set; }
    public int Pendientes => Comisiones.Count(x => x.Estatus == "PENDIENTE");
    public int Pagadas => Comisiones.Count(x => x.Estatus is "PAGADA" or "PAGADO");
    public int SinPorcentaje => Comisiones.Count(x => x.Base > 0 && x.Porcentaje <= 0);
    public decimal TotalSeleccionado => Comisiones.Where(x => x.IsSelected).Sum(x => Math.Max(x.Importe - x.Pago, 0m));

    public ComisionDesktopRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value) && value is not null)
            {
                FolioOperacion = value.Folio;
                ImporteAbono = Math.Max(value.Importe - value.Pago, 0m);
                OnPropertyChanged(nameof(FolioOperacion));
                OnPropertyChanged(nameof(ImporteAbono));
            }
        }
    }

    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    private Task ConsultarAsync() => ConsultarAsync(autoRecalcular: false);

    private async Task ConsultarAsync(bool autoRecalcular)
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPosComisionesService>();
            if (autoRecalcular)
                await RecalcularCoreAsync(service);

            var model = await service.TryGetAsync(FolioOperacion, string.IsNullOrWhiteSpace(FolioOperacion) ? FechaInicio : null, string.IsNullOrWhiteSpace(FolioOperacion) ? FechaFin : null)
                ?? new PosComisionesViewModel();
            TotalVenta = model.TotalVenta;
            TotalBase = model.TotalBase;
            TotalComision = model.TotalComision;
            TotalPagado = model.TotalPagado;
            Comisiones.Clear();
            foreach (var row in model.Comisiones)
            {
                var desktopRow = new ComisionDesktopRow(row);
                desktopRow.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ComisionDesktopRow.IsSelected))
                        OnPropertyChanged(nameof(TotalSeleccionado));
                };
                Comisiones.Add(desktopRow);
            }
            NotifyTotals();
            Message = $"{Comisiones.Count} comisiones cargadas.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible consultar comisiones. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RecalcularAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosComisionesService>();
        var actualizados = await RecalcularCoreAsync(service);
        Message = $"Comisiones recalculadas. Filas actualizadas: {actualizados}.";
        await ConsultarAsync(autoRecalcular: false);
    }

    private async Task<int> RecalcularCoreAsync(IPosComisionesService service)
    {
        if (!string.IsNullOrWhiteSpace(FolioOperacion))
            return await service.RecalcularAsync(FolioOperacion, _session.Usuario);

        if (FechaInicio.HasValue || FechaFin.HasValue)
        {
            var inicio = (FechaInicio ?? FechaFin)!.Value.Date;
            var fin = (FechaFin ?? FechaInicio)!.Value.Date;
            if (fin < inicio)
                (inicio, fin) = (fin, inicio);
            var total = 0;
            for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
                total += await service.RecalcularAsync(null, _session.Usuario, fecha);
            return total;
        }

        return await service.RecalcularAsync(null, _session.Usuario);
    }

    private async Task PagarSeleccionadaAsync()
    {
        if (SelectedRow is null)
        {
            Message = "Seleccione una comision.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosComisionesService>();
        var ok = await service.PayAsync(SelectedRow.Folio, _session.Usuario);
        Message = ok ? $"Comision pagada para el folio {SelectedRow.Folio}." : "No se pudo pagar la comision seleccionada.";
        await ConsultarAsync(autoRecalcular: false);
    }

    private async Task PagarSeleccionAsync()
    {
        var selected = Comisiones.Where(x => x.IsSelected).Select(x => x.Folio).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selected.Count == 0)
        {
            Message = "Seleccione al menos una comision pendiente.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosComisionesService>();
        var pagadas = 0;
        foreach (var folio in selected)
        {
            if (await service.PayAsync(folio, _session.Usuario))
                pagadas++;
        }
        Message = $"Comisiones pagadas: {pagadas} de {selected.Count}.";
        await ConsultarAsync(autoRecalcular: false);
    }

    private async Task AbonarAsync()
    {
        if (SelectedRow is null || ImporteAbono <= 0)
        {
            Message = "Seleccione una comision y capture un abono mayor a cero.";
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosPagosService>();
        var ok = await service.RegisterAsync(SelectedRow.Folio, ImporteAbono, _session.Usuario);
        Message = ok ? $"Pago aplicado a la comision del folio {SelectedRow.Folio}." : "No se pudo aplicar el abono.";
        await ConsultarAsync(autoRecalcular: false);
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalVenta));
        OnPropertyChanged(nameof(TotalBase));
        OnPropertyChanged(nameof(TotalComision));
        OnPropertyChanged(nameof(TotalPagado));
        OnPropertyChanged(nameof(Pendientes));
        OnPropertyChanged(nameof(Pagadas));
        OnPropertyChanged(nameof(SinPorcentaje));
        OnPropertyChanged(nameof(TotalSeleccionado));
    }
}

public sealed class ComisionDesktopRow(PosComisionRowViewModel source) : ObservableObject
{
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public string Folio => source.Folio;
    public string Ticket => source.Ticket;
    public string Fecha => source.Fecha;
    public string BeneficiarioTipo => source.BeneficiarioTipo;
    public string Beneficiario => source.Beneficiario;
    public string Staff => source.Staff;
    public string Taxista => string.IsNullOrWhiteSpace(source.Staff) ? source.Beneficiario : source.Staff;
    public string Unidad => source.Unidad;
    public string NumeroUnidad => source.NumeroUnidad;
    public string Hotel => source.Hotel;
    public string FormaPago => source.FormaPago;
    public string Vendedor => source.Vendedor;
    public int Pax => source.Pax;
    public string Transporte => source.Transporte;
    public decimal Venta => source.Venta;
    public decimal VentaArtesania => source.VentaArtesania;
    public decimal VentaFarmacia => source.VentaFarmacia;
    public decimal VentaTienda => source.VentaTienda;
    public decimal VentaJoyeria => source.VentaJoyeria;
    public decimal Base => source.Base;
    public decimal Porcentaje => source.Porcentaje;
    public decimal Importe => source.Importe;
    public decimal Pago => source.Pago;
    public string FechaPago => source.FechaPago;
    public string Estatus => source.Pago > 0 ? "PAGADA" : source.Estatus;
    public decimal DescuentoAplicado => source.DescuentoAplicado;
    public decimal DeduccionDejada => source.DeduccionDejada;
    public decimal BebidasCajas => source.DeduccionBebidas + source.DeduccionCajasRegalo;
    public decimal DeduccionReparacion => source.DeduccionReparacion;
    public decimal DeduccionDegustacion => source.DeduccionDegustacion;
}
