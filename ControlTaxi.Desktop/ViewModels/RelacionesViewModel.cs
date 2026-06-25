using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxi.Desktop.Services;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class RelacionesViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private readonly TicketPrintService _ticketService;
    private string _busqueda = string.Empty;
    private DateTime? _fechaInicio = DateTime.Today;
    private DateTime? _fechaFin = DateTime.Today;
    private PosRelacionRowViewModel? _selectedRelacion;
    private PosDejadaTicketViewModel? _currentTicket;
    private string _ticketPreview = string.Empty;
    private string _message = string.Empty;
    private bool _isBusy;

    public RelacionesViewModel(IServiceScopeFactory scopeFactory, DesktopSession session, TicketPrintService ticketService)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        _ticketService = ticketService;
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        GuardarRelacionCommand = new AsyncRelayCommand(GuardarRelacionAsync);
        PagarDejadaCommand = new AsyncRelayCommand(PagarDejadaAsync, () => HasAnyFolio);
        ImprimirTicketCommand = new RelayCommand(ImprimirTicket, () => !string.IsNullOrWhiteSpace(TicketPreview));
        ReimprimirTicketCommand = new RelayCommand(ImprimirTicket, () => !string.IsNullOrWhiteSpace(TicketPreview));
        GuardarTicketCommand = new AsyncRelayCommand(GuardarTicketAsync, () => CurrentTicket is not null);
        LimpiarCommand = new RelayCommand(LimpiarFormulario);
        _ = ConsultarAsync();
    }

    public ObservableCollection<PosRelacionRowViewModel> Relaciones { get; } = [];
    public ObservableCollection<string> Vendedores { get; } = [];

    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand GuardarRelacionCommand { get; }
    public AsyncRelayCommand PagarDejadaCommand { get; }
    public RelayCommand ImprimirTicketCommand { get; }
    public RelayCommand ReimprimirTicketCommand { get; }
    public AsyncRelayCommand GuardarTicketCommand { get; }
    public RelayCommand LimpiarCommand { get; }

    public string Busqueda { get => _busqueda; set => SetProperty(ref _busqueda, value); }
    public DateTime? FechaInicio { get => _fechaInicio; set => SetProperty(ref _fechaInicio, value); }
    public DateTime? FechaFin { get => _fechaFin; set => SetProperty(ref _fechaFin, value); }

    public string FolioControl { get; set; } = string.Empty;
    public string FolioApp { get; set; } = string.Empty;
    public string Fuente { get; set; } = string.Empty;
    public string FolioOperacion { get; set; } = string.Empty;
    public string FolioPos { get; set; } = string.Empty;
    public string Fecha { get; set; } = string.Empty;
    public string Gafete { get; set; } = string.Empty;
    public string Nacionalidad { get; set; } = string.Empty;
    public long TaxistaId { get; set; }
    public string TaxistaNombre { get; set; } = string.Empty;
    public string Vendedor { get; set; } = string.Empty;
    public string TransporteTipo { get; set; } = string.Empty;
    public decimal? Dejada { get; set; }
    public decimal Comision { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public string Hotel { get; set; } = string.Empty;
    public string Unidad { get; set; } = string.Empty;
    public string Placas { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public int Pax { get; set; }

    public bool HasAnyFolio =>
        !string.IsNullOrWhiteSpace(FolioControl) ||
        !string.IsNullOrWhiteSpace(FolioApp) ||
        !string.IsNullOrWhiteSpace(FolioOperacion) ||
        !string.IsNullOrWhiteSpace(FolioPos);

    public PosRelacionRowViewModel? SelectedRelacion
    {
        get => _selectedRelacion;
        set
        {
            if (SetProperty(ref _selectedRelacion, value) && value is not null)
                ApplyRelacion(value);
        }
    }

    public PosDejadaTicketViewModel? CurrentTicket
    {
        get => _currentTicket;
        private set
        {
            if (SetProperty(ref _currentTicket, value))
                GuardarTicketCommand.RaiseCanExecuteChanged();
        }
    }

    public string TicketPreview
    {
        get => _ticketPreview;
        private set
        {
            if (SetProperty(ref _ticketPreview, value))
            {
                ImprimirTicketCommand.RaiseCanExecuteChanged();
                ReimprimirTicketCommand.RaiseCanExecuteChanged();
            }
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
            var service = scope.ServiceProvider.GetRequiredService<IPosRelacionesService>();
            var model = await service.TryGetAsync(Busqueda, FechaInicio, FechaFin) ?? new PosRelacionesViewModel();
            Replace(Relaciones, model.Relaciones);
            Replace(Vendedores, model.Vendedores);
            Message = $"{Relaciones.Count} relaciones cargadas.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible consultar relaciones. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GuardarRelacionAsync()
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPosRelacionesService>();
            var ok = await service.SaveAsync(BuildModel(), _session.Usuario);
            Message = ok ? "Relacion guardada correctamente." : "No se pudo guardar la relacion.";
            if (ok)
                await ConsultarAsync();
        }
        catch (Exception ex)
        {
            Message = "No fue posible guardar la relacion. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PagarDejadaAsync()
    {
        try
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPosRelacionesService>();
            var ticket = await service.PayDejadaAsync(FolioControl, FolioApp, FolioOperacion, FolioPos, _session.Usuario);
            if (ticket is null)
            {
                Message = "No se pudo pagar la dejada. Revise importe o estatus.";
                return;
            }

            EnrichTicket(ticket);
            ticket.TicketTexto = _ticketService.BuildDejadaReceiptContent(ticket);
            CurrentTicket = ticket;
            TicketPreview = ticket.TicketTexto;
            Message = $"Dejada pagada. Ticket {ticket.Ticket}.";
            await ConsultarAsync();
        }
        catch (Exception ex)
        {
            Message = "No fue posible pagar la dejada. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ImprimirTicket()
    {
        if (string.IsNullOrWhiteSpace(TicketPreview))
            return;
        _ticketService.PrintText(TicketPreview);
    }

    private async Task GuardarTicketAsync()
    {
        if (CurrentTicket is null)
            return;
        var path = await _ticketService.SaveTicketAsync(CurrentTicket);
        if (!string.IsNullOrWhiteSpace(path))
            Message = $"Ticket guardado: {path}";
    }

    private PosRelacionesViewModel BuildModel() => new()
    {
        Busqueda = Busqueda,
        FechaInicio = FechaInicio,
        FechaFin = FechaFin,
        FolioControl = FolioControl,
        FolioApp = FolioApp,
        Fuente = Fuente,
        FolioOperacion = FolioOperacion,
        FolioPos = FolioPos,
        Fecha = Fecha,
        Gafete = Gafete,
        Nacionalidad = Nacionalidad,
        TaxistaId = TaxistaId,
        TaxistaNombre = TaxistaNombre,
        Vendedor = Vendedor,
        TransporteTipo = TransporteTipo,
        Dejada = Dejada,
        Comision = Comision,
        Observaciones = Observaciones
    };

    private void ApplyRelacion(PosRelacionRowViewModel row)
    {
        FolioControl = row.FolioControl;
        FolioApp = row.FolioApp;
        Fuente = row.Fuente;
        FolioOperacion = First(row.FolioOperacion, row.FolioOperacionSugerido);
        FolioPos = First(row.FolioPos, row.FolioPosSugerido);
        Fecha = row.Fecha;
        Gafete = row.Gafete;
        Nacionalidad = row.Nacionalidad;
        TaxistaId = row.TaxistaId;
        TaxistaNombre = row.TaxistaNombre;
        Vendedor = First(row.VendedorAsignado, row.Vendedor);
        TransporteTipo = row.TransporteTipo;
        Dejada = row.Dejada;
        Comision = row.Comision;
        Observaciones = row.Observaciones;
        Hotel = row.Hotel;
        Unidad = row.Unidad;
        Placas = row.Placas;
        Destino = row.Destino;
        Telefono = row.Telefono;
        Pax = row.Pax;
        NotifyForm();
    }

    private void EnrichTicket(PosDejadaTicketViewModel ticket)
    {
        ticket.Taxista = First(TaxistaNombre, ticket.Taxista);
        ticket.Vendedor = First(Vendedor, ticket.Vendedor);
        ticket.Gafete = First(Gafete, ticket.Gafete);
        ticket.Nacionalidad = First(Nacionalidad, ticket.Nacionalidad);
        ticket.Transporte = First(TransporteTipo, ticket.Transporte);
        ticket.Hotel = First(Hotel, ticket.Hotel);
        ticket.Unidad = First(Unidad, ticket.Unidad);
        ticket.Placas = First(Placas, ticket.Placas);
        ticket.Destino = First(Destino, ticket.Destino);
        ticket.Telefono = First(Telefono, ticket.Telefono);
        ticket.FechaViaje = First(Fecha, ticket.FechaViaje);
        if (Pax > 0)
            ticket.Pax = Pax;
        var dejada = Dejada.GetValueOrDefault();
        if (dejada > 0m)
            ticket.Importe = dejada;
        ticket.Busqueda = Busqueda;
        ticket.FechaInicio = FechaInicio;
        ticket.FechaFin = FechaFin;
    }

    private void LimpiarFormulario()
    {
        FolioControl = FolioApp = Fuente = FolioOperacion = FolioPos = Fecha = Gafete = Nacionalidad = string.Empty;
        TaxistaId = 0;
        TaxistaNombre = Vendedor = TransporteTipo = Observaciones = Hotel = Unidad = Placas = Destino = Telefono = string.Empty;
        Dejada = null;
        Comision = 0;
        Pax = 0;
        NotifyForm();
    }

    private void NotifyForm()
    {
        OnPropertyChanged(nameof(FolioControl));
        OnPropertyChanged(nameof(FolioApp));
        OnPropertyChanged(nameof(Fuente));
        OnPropertyChanged(nameof(FolioOperacion));
        OnPropertyChanged(nameof(FolioPos));
        OnPropertyChanged(nameof(Fecha));
        OnPropertyChanged(nameof(Gafete));
        OnPropertyChanged(nameof(Nacionalidad));
        OnPropertyChanged(nameof(TaxistaId));
        OnPropertyChanged(nameof(TaxistaNombre));
        OnPropertyChanged(nameof(Vendedor));
        OnPropertyChanged(nameof(TransporteTipo));
        OnPropertyChanged(nameof(Dejada));
        OnPropertyChanged(nameof(Comision));
        OnPropertyChanged(nameof(Observaciones));
        OnPropertyChanged(nameof(Hotel));
        OnPropertyChanged(nameof(Unidad));
        OnPropertyChanged(nameof(Placas));
        OnPropertyChanged(nameof(Destino));
        OnPropertyChanged(nameof(Telefono));
        OnPropertyChanged(nameof(Pax));
        OnPropertyChanged(nameof(HasAnyFolio));
        PagarDejadaCommand.RaiseCanExecuteChanged();
    }

    private static string First(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }
}
