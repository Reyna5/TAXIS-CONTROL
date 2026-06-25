using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.Services;

public sealed record ModuleDefinition(
    string Key,
    string Title,
    string Description,
    string Permission,
    string Icon);

public sealed class ModuleCatalog
{
    public IReadOnlyList<ModuleDefinition> Modules { get; } =
    [
        new("dashboard", "Dashboard", "Indicadores y accesos principales", string.Empty, "Inicio"),
        new("registro", "Registro diario", "Consulta y actualizacion de operaciones del dia", "RegistroDiario", "Registro"),
        new("registro-app", "Registro App", "Captura local de viajes de app movil", "RegistroDiario", "App"),
        new("relaciones", "Relaciones ticket-taxista", "Vinculacion de folios, tickets y dejadas", "Relaciones", "Relaciones"),
        new("gafetes", "Gafetes", "Asignacion, regreso y control de gafetes", "Gafetes", "Gafetes"),
        new("pagos", "Pagos", "Abonos y pagos por folio de operacion", "Comisiones", "Pagos"),
        new("ventas", "Ventas", "Punto de venta y productos relacionados", "Comisiones", "Ventas"),
        new("comisiones", "Comisiones", "Calculo, pago y consulta de comisiones", "Comisiones", "Comisiones"),
        new("gastos", "Gastos", "Captura y consulta de gastos por operacion", "Gastos", "Gastos"),
        new("cortes", "Cortes", "Cierre diario de caja", "Cortes", "Cortes"),
        new("transportes", "Transportes", "Catalogo de transportes", "Transportes", "Catalogo"),
        new("guias", "Guias", "Catalogo de guias", "Guias", "Catalogo"),
        new("taxistas", "Taxistas", "Catalogo de taxistas", "Taxistas", "Catalogo"),
        new("usuarios", "Usuarios y permisos", "Administracion de usuarios, roles y permisos", "Usuarios", "Usuarios"),
        new("reportes", "Reportes", "Operaciones, dejadas, comisiones y concentrados", "Reportes", "Reportes"),
        new("configuracion", "Configuracion", "Conexion local, bases de datos y sincronizacion opcional", string.Empty, "Config")
    ];
}

public sealed class ModuleDataService(IServiceScopeFactory scopeFactory)
{
    public async Task<ModuleLoadResult> LoadAsync(ModuleDefinition module, ModuleQuery query, string usuario)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        return module.Key switch
        {
            "dashboard" => BuildDashboard(provider),
            "registro" => await LoadRegistroAsync(provider, query),
            "registro-app" => await LoadRegistroAppAsync(provider, query),
            "relaciones" => await LoadRelacionesAsync(provider, query),
            "gafetes" => await LoadGafetesAsync(provider, query),
            "pagos" => await LoadPagosAsync(provider, query),
            "ventas" => await LoadVentasAsync(provider, query),
            "comisiones" => await LoadComisionesAsync(provider, query),
            "gastos" => await LoadGastosAsync(provider, query),
            "cortes" => await LoadCortesAsync(provider, query),
            "transportes" => await LoadTransportesAsync(provider),
            "guias" => await LoadGuiasAsync(provider),
            "taxistas" => await LoadTaxistasAsync(provider, query),
            "usuarios" => await LoadUsuariosAsync(provider, query),
            "reportes" => await LoadReportesAsync(provider, query),
            "configuracion" => ModuleLoadResult.Message("Use esta pantalla para editar el archivo local de configuracion y probar conexion."),
            _ => ModuleLoadResult.Message("Modulo no reconocido.")
        };
    }

    public async Task<ModuleActionResult> ExecuteActionAsync(string actionKey, ModuleQuery query, string usuario)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        return actionKey switch
        {
            "registro-guardar" => await UpdateRegistroAsync(provider, query, usuario),
            "registro-app-crear" => await CreateRegistroAppAsync(provider, query, usuario),
            "relaciones-guardar" => await SaveRelacionAsync(provider, query, usuario),
            "relaciones-pagar-dejada" => await PayDejadaAsync(provider, query, usuario),
            "gafetes-guardar" => await SaveGafeteAsync(provider, query, usuario),
            "gafetes-regreso" => await ReturnGafeteAsync(provider, query, usuario),
            "pagos-registrar" => await RegisterPagoAsync(provider, query, usuario),
            "gastos-guardar" => await SaveGastoAsync(provider, query, usuario),
            "cortes-cerrar" => await CloseCorteAsync(provider, query, usuario),
            "comisiones-recalcular" => await RecalculateCommissionsAsync(provider, query, usuario),
            "comisiones-pagar" => await PayCommissionAsync(provider, query, usuario),
            "transportes-guardar" => await SaveTransporteAsync(provider, query, usuario),
            "guias-guardar" => await SaveGuiaAsync(provider, query, usuario),
            "taxistas-guardar" => await SaveTaxistaAsync(provider, query, usuario),
            "usuarios-guardar" => await SaveUsuarioAsync(provider, query, usuario),
            _ => ModuleActionResult.Fail("Accion no reconocida.")
        };
    }

    private static ModuleLoadResult BuildDashboard(IServiceProvider provider)
    {
        var catalog = provider.GetRequiredService<ModuleCatalog>();
        return new ModuleLoadResult(
            "Dashboard Desktop",
            "Seleccione un modulo del menu lateral. Los servicios trabajan contra SQL Server local/red local y la sincronizacion externa es opcional.",
            RowProjection.FromObjects(catalog.Modules.Select(x => new
            {
                Modulo = x.Title,
                x.Description,
                Permiso = string.IsNullOrWhiteSpace(x.Permission) ? "Libre" : x.Permission
            })),
            null);
    }

    private static async Task<ModuleLoadResult> LoadRegistroAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosRegistroService>();
        var model = await service.TryGetAsync(query.Folio, query.FechaInicio, query.FechaFin);
        return new ModuleLoadResult(
            "Registro diario",
            $"Total: {model?.TotalGeneral:C} | Efectivo: {model?.TotalEfectivo:C} | Tarjeta: {model?.TotalTarjeta:C}",
            RowProjection.FromObjects(model?.Operaciones ?? []),
            model);
    }

    private static async Task<ModuleLoadResult> LoadRegistroAppAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosRegistroService>();
        var model = new PosRegistroAppViewModel
        {
            FechaOperacion = query.FechaTrabajo ?? DateTime.Now,
            TaxistaNombre = query.Nombre,
            Gafete = query.Gafete,
            Hotel = query.Hotel,
            Destino = query.Destino,
            Pax = query.Pax <= 0 ? 1 : query.Pax,
            Total = query.Importe,
            MetodoPago = string.IsNullOrWhiteSpace(query.MetodoPago) ? "Efectivo" : query.MetodoPago,
            Notas = query.Observaciones
        };
        model.Taxistas = await service.SearchTaxistasAsync(query.Busqueda, 20);
        model.Tarifas = await service.SearchTarifasAsync(query.Busqueda, 50);
        model.Hoteles = await service.SearchHotelesAsync(query.Busqueda, 50);
        return new ModuleLoadResult(
            "Registro App",
            "Use los campos de captura y el boton Crear registro local. Las busquedas usan SQL local y API opcional si esta configurada.",
            RowProjection.FromObjects(model.Taxistas),
            model);
    }

    private static async Task<ModuleLoadResult> LoadRelacionesAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosRelacionesService>();
        var model = await service.TryGetAsync(query.Busqueda, query.FechaInicio, query.FechaFin);
        return new ModuleLoadResult("Relaciones ticket-taxista", $"{model?.Relaciones.Count ?? 0} relaciones", RowProjection.FromObjects(model?.Relaciones ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadGafetesAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosGafetesService>();
        var model = await service.TryGetAsync(query.FechaInicio, query.FechaFin, query.SoloPendientes);
        return new ModuleLoadResult("Gafetes", $"{model?.TotalFilas ?? 0} filas", RowProjection.FromStringRows([], model?.Filas ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadPagosAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosPagosService>();
        var model = await service.TryGetAsync(query.Folio);
        return new ModuleLoadResult("Pagos", $"Saldo: {model?.Saldo:C}", RowProjection.FromObjects(model?.Pagos ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadVentasAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosVentasService>();
        var model = await service.TryGetAsync(query.Busqueda);
        return new ModuleLoadResult("Ventas", $"{model?.ResultadosBusqueda.Count ?? 0} productos encontrados", RowProjection.FromObjects(model?.ResultadosBusqueda ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadComisionesAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosComisionesService>();
        var model = await service.TryGetAsync(query.Folio, query.FechaInicio, query.FechaFin);
        return new ModuleLoadResult("Comisiones", $"Total comision: {model?.TotalComision:C} | Pagado: {model?.TotalPagado:C}", RowProjection.FromObjects(model?.Comisiones ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadGastosAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosGastosService>();
        var model = await service.TryGetAsync(query.Folio);
        return new ModuleLoadResult("Gastos", $"Total dia: {model?.TotalDia:C}", RowProjection.FromStringRows([], model?.Filas ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadCortesAsync(IServiceProvider provider, ModuleQuery query)
    {
        var service = provider.GetRequiredService<IPosCortesService>();
        var model = await service.TryGetAsync(query.FechaTrabajo);
        return new ModuleLoadResult("Cortes", $"Total dia: {model?.TotalDia:C} | Cerrado: {model?.Cerrado}", RowProjection.FromObjects(model is null ? [] : [model]), model);
    }

    private static async Task<ModuleLoadResult> LoadTransportesAsync(IServiceProvider provider)
    {
        var model = await provider.GetRequiredService<IPosCatalogosService>().TryGetTransportesAsync();
        return new ModuleLoadResult(model?.Titulo ?? "Transportes", model?.Descripcion ?? string.Empty, RowProjection.FromStringRows(model?.Columnas ?? [], model?.Filas ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadGuiasAsync(IServiceProvider provider)
    {
        var model = await provider.GetRequiredService<IPosCatalogosService>().TryGetGuiasAsync();
        return new ModuleLoadResult(model?.Titulo ?? "Guias", model?.Descripcion ?? string.Empty, RowProjection.FromStringRows(model?.Columnas ?? [], model?.Filas ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadTaxistasAsync(IServiceProvider provider, ModuleQuery query)
    {
        var model = await provider.GetRequiredService<IPosCatalogosService>().TryGetTaxistasAsync(query.Busqueda);
        return new ModuleLoadResult(model?.Titulo ?? "Taxistas", model?.Descripcion ?? string.Empty, RowProjection.FromStringRows(model?.Columnas ?? [], model?.Filas ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadUsuariosAsync(IServiceProvider provider, ModuleQuery query)
    {
        var model = await provider.GetRequiredService<IPosUsuariosService>().TryGetAsync(query.UsuarioEdicion);
        return new ModuleLoadResult("Usuarios y permisos", $"{model?.Usuarios.Count ?? 0} usuarios", RowProjection.FromObjects(model?.Usuarios ?? []), model);
    }

    private static async Task<ModuleLoadResult> LoadReportesAsync(IServiceProvider provider, ModuleQuery query)
    {
        var relaciones = provider.GetRequiredService<IPosRelacionesService>();
        var comisiones = provider.GetRequiredService<IPosComisionesService>();
        var dejadas = await relaciones.GetReporteDejadasAsync(query.FechaInicio, query.FechaFin, query.Busqueda);
        var pagos = await comisiones.GetPagosReporteAsync(query.FechaInicio, query.FechaFin);
        var resumen = await relaciones.GetCamionesResumenAsync(query.FechaInicio, query.FechaFin);
        var rows = dejadas.Cast<object>().Concat(pagos).Concat(resumen).ToList();
        return new ModuleLoadResult("Reportes", $"Dejadas: {dejadas.Count} | Pagos comisiones: {pagos.Count} | Camiones: {resumen.Count}", new ObservableCollection<object>(rows), null);
    }

    private static async Task<ModuleActionResult> UpdateRegistroAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosRegistroService>().UpdateAsync(query.Folio, query.Staff, query.Pax, query.FechaTrabajo ?? DateTime.Today, usuario);
        return ModuleActionResult.From(ok, "Registro actualizado.");
    }

    private static async Task<ModuleActionResult> CreateRegistroAppAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var model = new PosRegistroAppViewModel
        {
            FechaOperacion = query.FechaTrabajo ?? DateTime.Now,
            TaxistaNombre = query.Nombre,
            TaxistaId = query.TaxistaId,
            Gafete = query.Gafete,
            Telefono = query.Telefono,
            Nacionalidad = query.Nacionalidad,
            Placas = query.Placas,
            Modelo = query.Modelo,
            Unidad = query.Unidad,
            Hotel = query.Hotel,
            Origen = query.Origen,
            Sitio = query.Sitio,
            Destino = query.Destino,
            Pax = query.Pax <= 0 ? 1 : query.Pax,
            TipoOperacion = string.IsNullOrWhiteSpace(query.TipoOperacion) ? "DEJADA" : query.TipoOperacion,
            Total = query.Importe,
            MetodoPago = string.IsNullOrWhiteSpace(query.MetodoPago) ? "Efectivo" : query.MetodoPago,
            Notas = query.Observaciones
        };
        var folio = await provider.GetRequiredService<IPosRegistroService>().CreateAppAsync(model, usuario);
        return string.IsNullOrWhiteSpace(folio)
            ? ModuleActionResult.Fail("No se pudo crear el registro.")
            : ModuleActionResult.Ok($"Registro creado: {folio}");
    }

    private static async Task<ModuleActionResult> SaveRelacionAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var model = new PosRelacionesViewModel
        {
            FolioControl = query.FolioControl,
            FolioApp = query.FolioApp,
            Fuente = query.Fuente,
            FolioOperacion = query.Folio,
            FolioPos = query.FolioPos,
            Gafete = query.Gafete,
            TaxistaId = query.TaxistaId ?? 0,
            TaxistaNombre = query.Nombre,
            Vendedor = query.Staff,
            TransporteTipo = query.TipoOperacion,
            Dejada = query.Importe,
            Comision = query.Comision,
            Observaciones = query.Observaciones
        };
        var ok = await provider.GetRequiredService<IPosRelacionesService>().SaveAsync(model, usuario);
        return ModuleActionResult.From(ok, "Relacion guardada.");
    }

    private static async Task<ModuleActionResult> PayDejadaAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ticket = await provider.GetRequiredService<IPosRelacionesService>().PayDejadaAsync(query.FolioControl, query.FolioApp, query.Folio, query.FolioPos, usuario);
        return ticket is null ? ModuleActionResult.Fail("No se pudo pagar la dejada.") : ModuleActionResult.Ok(ticket.TicketTexto);
    }

    private static async Task<ModuleActionResult> SaveGafeteAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosGafetesService>().InsertAsync(query.Matricula, query.Gafete, query.FolioOperacion, query.Movimiento, usuario);
        return ModuleActionResult.From(ok, "Gafete guardado.");
    }

    private static async Task<ModuleActionResult> ReturnGafeteAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosGafetesService>().MarcarRegresoAsync(query.Gafete, query.FolioOperacion, usuario);
        return ModuleActionResult.From(ok, "Regreso de gafete registrado.");
    }

    private static async Task<ModuleActionResult> RegisterPagoAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosPagosService>().RegisterAsync(query.Folio, query.Importe, usuario);
        return ModuleActionResult.From(ok, "Pago registrado.");
    }

    private static async Task<ModuleActionResult> SaveGastoAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosGastosService>().UpdateAsync(query.Folio, query.Importe, query.Concepto, query.Observaciones, usuario);
        return ModuleActionResult.From(ok, "Gasto guardado.");
    }

    private static async Task<ModuleActionResult> CloseCorteAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosCortesService>().CloseAsync(usuario, query.FechaTrabajo ?? DateTime.Today);
        return ModuleActionResult.From(ok, "Corte cerrado.");
    }

    private static async Task<ModuleActionResult> RecalculateCommissionsAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var count = await provider.GetRequiredService<IPosComisionesService>().RecalcularAsync(query.Folio, usuario, query.FechaTrabajo);
        return ModuleActionResult.Ok($"Comisiones recalculadas: {count}");
    }

    private static async Task<ModuleActionResult> PayCommissionAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosComisionesService>().PayAsync(query.Folio, usuario);
        return ModuleActionResult.From(ok, "Comision pagada.");
    }

    private static async Task<ModuleActionResult> SaveTransporteAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosCatalogosService>().SaveTransporteAsync(query.Clave, query.Nombre, query.Minimo, query.Maximo, query.Comision, query.DescEfectivo, query.DescTarjeta, query.DescAmex, usuario);
        return ModuleActionResult.From(ok, "Transporte guardado.");
    }

    private static async Task<ModuleActionResult> SaveGuiaAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosCatalogosService>().SaveGuiaAsync(query.Clave, query.Nombre, query.Telefono, query.Comision, query.Estatus, usuario);
        return ModuleActionResult.From(ok, "Guia guardada.");
    }

    private static async Task<ModuleActionResult> SaveTaxistaAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var ok = await provider.GetRequiredService<IPosCatalogosService>().SaveTaxistaAsync(query.Clave, query.Nombre, query.Telefono, query.Unidad, query.Placas, query.Estatus, usuario);
        return ModuleActionResult.From(ok, "Taxista guardado.");
    }

    private static async Task<ModuleActionResult> SaveUsuarioAsync(IServiceProvider provider, ModuleQuery query, string usuario)
    {
        var permisos = query.Permisos.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var ok = await provider.GetRequiredService<IPosUsuariosService>().SaveAsync(query.UsuarioEdicion, query.Password, query.Rol, query.Estatus, permisos, usuario);
        return ModuleActionResult.From(ok, "Usuario guardado.");
    }
}

public sealed record ModuleLoadResult(string Title, string Summary, ObservableCollection<object> Rows, object? RawModel)
{
    public static ModuleLoadResult Message(string message) => new("Informacion", message, [], null);
}

public sealed record ModuleActionResult(bool Success, string Message)
{
    public static ModuleActionResult Ok(string message) => new(true, message);
    public static ModuleActionResult Fail(string message) => new(false, message);
    public static ModuleActionResult From(bool success, string successMessage) =>
        success ? Ok(successMessage) : Fail("La operacion no se pudo completar.");
}

public sealed class ModuleQuery : ObservableObject
{
    private string _busqueda = string.Empty;
    private string _folio = string.Empty;
    private DateTime? _fechaInicio = DateTime.Today;
    private DateTime? _fechaFin = DateTime.Today;
    private DateTime? _fechaTrabajo = DateTime.Today;
    private decimal _importe;
    private decimal _comision;
    private int _pax = 1;
    private bool _soloPendientes;

    public string Busqueda { get => _busqueda; set => SetProperty(ref _busqueda, value); }
    public string Folio { get => _folio; set => SetProperty(ref _folio, value); }
    public DateTime? FechaInicio { get => _fechaInicio; set => SetProperty(ref _fechaInicio, value); }
    public DateTime? FechaFin { get => _fechaFin; set => SetProperty(ref _fechaFin, value); }
    public DateTime? FechaTrabajo { get => _fechaTrabajo; set => SetProperty(ref _fechaTrabajo, value); }
    public decimal Importe { get => _importe; set => SetProperty(ref _importe, value); }
    public decimal Comision { get => _comision; set => SetProperty(ref _comision, value); }
    public int Pax { get => _pax; set => SetProperty(ref _pax, value); }
    public bool SoloPendientes { get => _soloPendientes; set => SetProperty(ref _soloPendientes, value); }

    public string Staff { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Gafete { get; set; } = string.Empty;
    public string Hotel { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public string MetodoPago { get; set; } = "Efectivo";
    public string Observaciones { get; set; } = string.Empty;
    public int? TaxistaId { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string Nacionalidad { get; set; } = string.Empty;
    public string Placas { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Unidad { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public string Sitio { get; set; } = string.Empty;
    public string TipoOperacion { get; set; } = "DEJADA";
    public string FolioControl { get; set; } = string.Empty;
    public string FolioApp { get; set; } = string.Empty;
    public string Fuente { get; set; } = string.Empty;
    public string FolioPos { get; set; } = string.Empty;
    public int Matricula { get; set; }
    public long? FolioOperacion { get; set; }
    public string Movimiento { get; set; } = "A";
    public string Concepto { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public decimal Minimo { get; set; }
    public decimal Maximo { get; set; }
    public decimal DescEfectivo { get; set; }
    public decimal DescTarjeta { get; set; }
    public decimal DescAmex { get; set; }
    public string Estatus { get; set; } = "Activo";
    public string UsuarioEdicion { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Rol { get; set; } = "Cajero";
    public string Permisos { get; set; } = string.Empty;
}
