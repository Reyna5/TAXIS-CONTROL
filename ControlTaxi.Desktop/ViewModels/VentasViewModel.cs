using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class VentasViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private string _productoBusqueda = string.Empty;
    private string _cartOwner = string.Empty;
    private string _message = string.Empty;
    private bool _isBusy;
    private PosVentaDetalleViewModel? _selectedResultado;
    private PosVentaDetalleViewModel? _selectedCartItem;
    private PosVentaDetalleViewModel? _selectedRemisionItem;
    private PosVentasViewModel? _remisionDetalle;
    private string _remisionFolio = string.Empty;

    public VentasViewModel(IServiceScopeFactory scopeFactory, DesktopSession session)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        BuscarCommand = new AsyncRelayCommand(BuscarAsync);
        AgregarSeleccionadoCommand = new AsyncRelayCommand(AgregarSeleccionadoAsync, () => SelectedResultado is not null);
        BorrarLineaCommand = new RelayCommand(BorrarLinea, () => SelectedCartItem is not null);
        NuevaVentaCommand = new RelayCommand(NuevaVenta);
        CobrarCommand = new AsyncRelayCommand(CobrarAsync, () => CartItems.Count > 0);
        AbrirRemisionCommand = new AsyncRelayCommand(AbrirRemisionAsync, () => !string.IsNullOrWhiteSpace(RemisionFolio));
        VerProductosRemisionCommand = new AsyncRelayCommand(VerProductosRemisionAsync, () => !string.IsNullOrWhiteSpace(FolioRegistroRemision));
        VolverVentaCommand = new RelayCommand(() => RemisionDetalle = null);

        Usuario = _session.Usuario;
        TipoCambio = "19.50";
        Cantidad = 1;
        Pax = 1;
        _ = BuscarAsync();
    }

    public ObservableCollection<PosVentaDetalleViewModel> ResultadosBusqueda { get; } = [];
    public ObservableCollection<PosVentaDetalleViewModel> CartItems { get; } = [];
    public ObservableCollection<PosVentaDetalleViewModel> RemisionProductos { get; } = [];
    public ObservableCollection<PosVentaGafeteRelacionViewModel> GafeteVentas { get; } = [];

    public AsyncRelayCommand BuscarCommand { get; }
    public AsyncRelayCommand AgregarSeleccionadoCommand { get; }
    public RelayCommand BorrarLineaCommand { get; }
    public RelayCommand NuevaVentaCommand { get; }
    public AsyncRelayCommand CobrarCommand { get; }
    public AsyncRelayCommand AbrirRemisionCommand { get; }
    public AsyncRelayCommand VerProductosRemisionCommand { get; }
    public RelayCommand VolverVentaCommand { get; }

    public string ProductoBusqueda
    {
        get => _productoBusqueda;
        set => SetProperty(ref _productoBusqueda, value);
    }

    public string Vendedor { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Usuario { get; set; }
    public string FolioControl { get; set; } = string.Empty;
    public string FolioApp { get; set; } = string.Empty;
    public string Gafete { get; set; } = string.Empty;
    public string TransporteTipo { get; set; } = "WEB";
    public int GuiaMatricula { get; set; }
    public long TaxistaId { get; set; }
    public string TaxistaNombre { get; set; } = string.Empty;
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public int Pax { get; set; }
    public decimal Efectivo { get; set; }
    public decimal Tarjeta { get; set; }
    public decimal Dolares { get; set; }
    public string TipoCambio { get; set; }
    public string RemisionFolio
    {
        get => _remisionFolio;
        set
        {
            if (SetProperty(ref _remisionFolio, value))
                AbrirRemisionCommand.RaiseCanExecuteChanged();
        }
    }
    public string TodayText => DateTime.Today.ToString("dd/MM/yyyy");
    public string FolioRegistroRemision => RemisionDetalle?.FolioRegistro ?? string.Empty;

    public decimal Subtotal => CartItems.Sum(x => x.Precio);
    public decimal Iva => CartItems.Sum(x => x.Iva);
    public decimal Total => CartItems.Sum(x => x.Importe);

    public PosVentaDetalleViewModel? SelectedResultado
    {
        get => _selectedResultado;
        set
        {
            if (SetProperty(ref _selectedResultado, value))
            {
                if (value is not null)
                    ProductoId = value.ProductoId;
                AgregarSeleccionadoCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public PosVentaDetalleViewModel? SelectedCartItem
    {
        get => _selectedCartItem;
        set
        {
            if (SetProperty(ref _selectedCartItem, value))
                BorrarLineaCommand.RaiseCanExecuteChanged();
        }
    }

    public PosVentaDetalleViewModel? SelectedRemisionItem
    {
        get => _selectedRemisionItem;
        set
        {
            if (SetProperty(ref _selectedRemisionItem, value) && value is not null && !string.IsNullOrWhiteSpace(value.Referencia))
            {
                RemisionFolio = value.Referencia;
            }
        }
    }

    public PosVentasViewModel? RemisionDetalle
    {
        get => _remisionDetalle;
        private set
        {
            if (SetProperty(ref _remisionDetalle, value))
            {
                OnPropertyChanged(nameof(FolioRegistroRemision));
                VerProductosRemisionCommand.RaiseCanExecuteChanged();
            }
        }
    }

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

    private async Task BuscarAsync()
    {
        try
        {
            IsBusy = true;
            Message = "Consultando ventas y productos...";
            var model = await WithVentasAsync(service => service.TryGetAsync(ProductoBusqueda)) ?? new PosVentasViewModel { ProductoBusqueda = ProductoBusqueda };
            ApplyModel(model);
            Message = "Consulta actualizada.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible consultar ventas/productos. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyModel(PosVentasViewModel model)
    {
        var currentOwner = NormalizeCartOwner(model.ProductoBusqueda);
        if (CartItems.Count > 0 && !string.Equals(_cartOwner, currentOwner, StringComparison.OrdinalIgnoreCase))
            CartItems.Clear();

        _cartOwner = currentOwner;
        ProductoBusqueda = model.ProductoBusqueda;
        Vendedor = model.Vendedor;
        Cliente = model.Cliente;
        Usuario = string.IsNullOrWhiteSpace(model.Usuario) || string.Equals(model.Usuario, "WEB", StringComparison.OrdinalIgnoreCase)
            ? _session.Usuario
            : model.Usuario;
        FolioControl = model.FolioControl;
        FolioApp = model.FolioApp;
        Gafete = model.Gafete;
        TransporteTipo = string.IsNullOrWhiteSpace(model.TransporteTipo) ? "WEB" : model.TransporteTipo;
        GuiaMatricula = model.GuiaMatricula;
        TaxistaId = model.TaxistaId;
        TaxistaNombre = model.TaxistaNombre;
        ProductoId = model.ProductoId;
        Cantidad = Math.Max(model.Cantidad, 1);
        Pax = Math.Max(model.Pax, 1);
        Efectivo = model.Efectivo;
        Tarjeta = model.Tarjeta;
        Dolares = model.Dolares;
        TipoCambio = string.IsNullOrWhiteSpace(model.TipoCambio) ? "19.50" : model.TipoCambio;

        ResultadosBusqueda.Clear();
        foreach (var item in model.ResultadosBusqueda)
            ResultadosBusqueda.Add(item);

        if (CartItems.Count == 0 && model.Items.Count > 0 && model.ResultadosBusqueda.Count == 0)
        {
            foreach (var item in model.Items)
                CartItems.Add(CloneLine(item));
        }

        GafeteVentas.Clear();
        foreach (var row in model.GafeteVentas)
            GafeteVentas.Add(row);

        NotifySaleFieldsChanged();
        RefreshTotals();
    }

    private async Task AgregarSeleccionadoAsync()
    {
        if (SelectedResultado is null)
            return;

        var product = await WithVentasAsync(service => service.TryGetProductAsync(SelectedResultado.ProductoId));
        if (product is null)
        {
            Message = "No se encontro el producto seleccionado.";
            return;
        }

        AddOrIncrease(product, Math.Max(Cantidad, 1));
        Message = "Producto agregado al ticket.";
    }

    private void BorrarLinea()
    {
        if (SelectedCartItem is null)
            return;

        CartItems.Remove(SelectedCartItem);
        SelectedCartItem = null;
        RefreshTotals();
        Message = "Linea eliminada del ticket.";
    }

    private void NuevaVenta()
    {
        CartItems.Clear();
        ResultadosBusqueda.Clear();
        RemisionProductos.Clear();
        GafeteVentas.Clear();
        RemisionDetalle = null;
        ProductoBusqueda = string.Empty;
        _cartOwner = string.Empty;
        Efectivo = 0;
        Tarjeta = 0;
        Dolares = 0;
        RefreshTotals();
        Message = "Nuevo ticket listo.";
    }

    private async Task CobrarAsync()
    {
        try
        {
            IsBusy = true;
            var model = BuildSaleModel();
            var folio = await WithVentasAsync(service => service.CreateAsync(model));
            if (string.IsNullOrWhiteSpace(folio))
            {
                Message = "No se pudo guardar la venta: revise producto, vendedor, importes y corte.";
                return;
            }

            CartItems.Clear();
            RefreshTotals();
            Message = $"Venta guardada con folio {folio}.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible cobrar la venta. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AbrirRemisionAsync()
    {
        if (string.IsNullOrWhiteSpace(RemisionFolio))
            return;

        try
        {
            IsBusy = true;
            RemisionDetalle = await WithVentasAsync(service => service.TryGetAsync(RemisionFolio));
            if (RemisionDetalle is null)
            {
                Message = "No se encontro la remision.";
                return;
            }

            RemisionProductos.Clear();
            Message = $"Remision {RemisionFolio} cargada.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible abrir la remision. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task VerProductosRemisionAsync()
    {
        if (string.IsNullOrWhiteSpace(FolioRegistroRemision))
            return;

        try
        {
            IsBusy = true;
            RemisionProductos.Clear();
            var products = await WithVentasAsync(service => service.TryGetRemisionProductosAsync(FolioRegistroRemision));
            foreach (var product in products)
                RemisionProductos.Add(product);
            Message = $"Productos de remision {FolioRegistroRemision} cargados.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible cargar productos de remision. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private PosVentasViewModel BuildSaleModel() => new()
    {
        Vendedor = Vendedor,
        Cliente = Cliente,
        Usuario = string.IsNullOrWhiteSpace(Usuario) ? _session.Usuario : Usuario,
        FolioControl = FolioControl,
        FolioApp = FolioApp,
        Gafete = Gafete,
        TransporteTipo = string.IsNullOrWhiteSpace(TransporteTipo) ? "WEB" : TransporteTipo,
        GuiaMatricula = GuiaMatricula,
        TaxistaId = TaxistaId,
        TaxistaNombre = TaxistaNombre,
        ProductoBusqueda = ProductoBusqueda,
        ProductoId = ProductoId,
        Cantidad = Math.Max(Cantidad, 1),
        Pax = Math.Max(Pax, 1),
        Efectivo = Efectivo,
        Tarjeta = Tarjeta,
        Dolares = Dolares,
        TipoCambio = string.IsNullOrWhiteSpace(TipoCambio) ? "19.50" : TipoCambio,
        Items = CartItems.Select(CloneLine).ToList()
    };

    private void AddOrIncrease(PosVentaDetalleViewModel item, int quantity)
    {
        var existing = CartItems.FirstOrDefault(x => x.ProductoId == item.ProductoId);
        if (existing is null)
        {
            var line = CloneLine(item);
            line.Cantidad = quantity;
            line.Precio *= quantity;
            line.Iva *= quantity;
            line.Importe *= quantity;
            CartItems.Add(line);
        }
        else
        {
            var currentQuantity = Math.Max(existing.Cantidad, 1);
            var unitPrice = existing.Precio / currentQuantity;
            var unitIva = existing.Iva / currentQuantity;
            existing.Cantidad += quantity;
            existing.Precio = unitPrice * existing.Cantidad;
            existing.Iva = unitIva * existing.Cantidad;
            existing.Importe = existing.Precio + existing.Iva;
        }

        RefreshTotals();
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Iva));
        OnPropertyChanged(nameof(Total));
        CobrarCommand.RaiseCanExecuteChanged();
    }

    private void NotifySaleFieldsChanged()
    {
        OnPropertyChanged(nameof(ProductoBusqueda));
        OnPropertyChanged(nameof(Vendedor));
        OnPropertyChanged(nameof(Cliente));
        OnPropertyChanged(nameof(Usuario));
        OnPropertyChanged(nameof(FolioControl));
        OnPropertyChanged(nameof(FolioApp));
        OnPropertyChanged(nameof(Gafete));
        OnPropertyChanged(nameof(TransporteTipo));
        OnPropertyChanged(nameof(GuiaMatricula));
        OnPropertyChanged(nameof(TaxistaId));
        OnPropertyChanged(nameof(TaxistaNombre));
        OnPropertyChanged(nameof(ProductoId));
        OnPropertyChanged(nameof(Cantidad));
        OnPropertyChanged(nameof(Pax));
        OnPropertyChanged(nameof(Efectivo));
        OnPropertyChanged(nameof(Tarjeta));
        OnPropertyChanged(nameof(Dolares));
        OnPropertyChanged(nameof(TipoCambio));
    }

    private static PosVentaDetalleViewModel CloneLine(PosVentaDetalleViewModel source) => new()
    {
        ProductoId = source.ProductoId,
        Cantidad = source.Cantidad,
        Producto = source.Producto,
        Referencia = source.Referencia,
        Factura = source.Factura,
        OrigenVenta = source.OrigenVenta,
        FolioRegistro = source.FolioRegistro,
        FechaVenta = source.FechaVenta,
        GafetesAsignados = source.GafetesAsignados,
        Precio = source.Precio,
        Iva = source.Iva,
        Importe = source.Importe,
        Departamento = source.Departamento
    };

    private static string NormalizeCartOwner(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<T> WithVentasAsync<T>(Func<IPosVentasService, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosVentasService>();
        return await action(service);
    }
}
