using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Data;
using ControlTaxiWeb.Models;
using ControlTaxiWeb.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class PortalViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private PortalDatabase _selectedDatabase = PortalDatabase.CompuadmoPlaza;
    private DateTime _selectedDate = DateTime.Today;
    private string _query = string.Empty;
    private string _activeSection = "Dashboard";
    private string _message = string.Empty;
    private bool _isBusy;
    private DashboardMetricsViewModel? _metrics;
    private CreateOperationInputModel _input = new();

    public PortalViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        DatabaseOptions = Enum.GetValues<PortalDatabase>();
        ShowDashboardCommand = new AsyncRelayCommand(LoadDashboardAsync);
        ShowOperationsCommand = new AsyncRelayCommand(LoadOperationsAsync);
        ShowCommissionsCommand = new AsyncRelayCommand(LoadCommissionsAsync);
        ShowVendorsCommand = new AsyncRelayCommand(LoadVendorsAsync);
        ShowProductsCommand = new AsyncRelayCommand(LoadProductsAsync);
        ShowCatalogsCommand = new AsyncRelayCommand(LoadCatalogsAsync);
        ShowCreateOperationCommand = new AsyncRelayCommand(LoadCreateOperationAsync);
        SaveOperationCommand = new AsyncRelayCommand(SaveOperationAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshActiveAsync);
        _ = LoadDashboardAsync();
    }

    public IReadOnlyList<PortalDatabase> DatabaseOptions { get; }
    public ObservableCollection<OperationRowViewModel> RecentOperations { get; } = [];
    public ObservableCollection<OperationRowViewModel> Operations { get; } = [];
    public ObservableCollection<CommissionRowViewModel> Commissions { get; } = [];
    public ObservableCollection<VendorRowViewModel> Vendors { get; } = [];
    public ObservableCollection<ProductRowViewModel> Products { get; } = [];
    public ObservableCollection<GuideRowViewModel> Guides { get; } = [];
    public ObservableCollection<TransportRowViewModel> Transports { get; } = [];

    public AsyncRelayCommand ShowDashboardCommand { get; }
    public AsyncRelayCommand ShowOperationsCommand { get; }
    public AsyncRelayCommand ShowCommissionsCommand { get; }
    public AsyncRelayCommand ShowVendorsCommand { get; }
    public AsyncRelayCommand ShowProductsCommand { get; }
    public AsyncRelayCommand ShowCatalogsCommand { get; }
    public AsyncRelayCommand ShowCreateOperationCommand { get; }
    public AsyncRelayCommand SaveOperationCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public PortalDatabase SelectedDatabase
    {
        get => _selectedDatabase;
        set
        {
            if (SetProperty(ref _selectedDatabase, value))
            {
                Input.Database = value;
                _ = RefreshActiveAsync();
            }
        }
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value.Date);
    }

    public string Query
    {
        get => _query;
        set => SetProperty(ref _query, value);
    }

    public string ActiveSection
    {
        get => _activeSection;
        private set => SetProperty(ref _activeSection, value);
    }

    public DashboardMetricsViewModel? Metrics
    {
        get => _metrics;
        private set => SetProperty(ref _metrics, value);
    }

    public CreateOperationInputModel Input
    {
        get => _input;
        private set => SetProperty(ref _input, value);
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

    private async Task LoadDashboardAsync()
    {
        await RunAsync("Dashboard", async service =>
        {
            var model = await service.GetDashboardAsync(SelectedDatabase, SelectedDate, CancellationToken.None);
            Metrics = model.Metrics;
            Replace(RecentOperations, model.RecentOperations);
            Message = "Dashboard Portal actualizado.";
        });
    }

    private async Task LoadOperationsAsync()
    {
        await RunAsync("Operaciones", async service =>
        {
            var model = await service.GetOperationsAsync(SelectedDatabase, SelectedDate, Query, CancellationToken.None);
            Replace(Operations, model.Operations);
            Message = $"{model.Operations.Count} operaciones encontradas.";
        });
    }

    private async Task LoadCommissionsAsync()
    {
        await RunAsync("Comisiones", async service =>
        {
            var model = await service.GetCommissionsAsync(SelectedDatabase, Query, CancellationToken.None);
            Replace(Commissions, model.Commissions);
            Message = $"{model.Commissions.Count} comisiones encontradas.";
        });
    }

    private async Task LoadVendorsAsync()
    {
        await RunAsync("Vendedores", async service =>
        {
            var model = await service.GetVendorsAsync(SelectedDatabase, Query, CancellationToken.None);
            Replace(Vendors, model.Vendors);
            Message = $"{model.Vendors.Count} vendedores encontrados.";
        });
    }

    private async Task LoadProductsAsync()
    {
        await RunAsync("Productos", async service =>
        {
            var model = await service.GetProductsAsync(SelectedDatabase, Query, CancellationToken.None);
            Replace(Products, model.Products);
            Message = $"{model.Products.Count} productos encontrados.";
        });
    }

    private async Task LoadCatalogsAsync()
    {
        await RunAsync("Catalogos", async service =>
        {
            var model = await service.GetCatalogsAsync(SelectedDatabase, CancellationToken.None);
            Replace(Guides, model.Guides);
            Replace(Transports, model.Transports);
            Message = "Catalogos Portal actualizados.";
        });
    }

    private async Task LoadCreateOperationAsync()
    {
        await RunAsync("Nueva operacion", async service =>
        {
            var model = await service.GetCreateOperationAsync(SelectedDatabase, CancellationToken.None, input: Input);
            Input = model.Input;
            Replace(Vendors, model.Vendors);
            Replace(Guides, model.Guides);
            Replace(Transports, model.Transports);
            Message = model.SuccessMessage ?? "Formulario de nueva operacion listo.";
        });
    }

    private async Task SaveOperationAsync()
    {
        await RunAsync("Nueva operacion", async service =>
        {
            Input.Database = SelectedDatabase;
            if (Input.SaleDate == default)
                Input.SaleDate = DateTime.Now;

            var folio = await service.CreateOperationAsync(Input, CancellationToken.None);
            Message = $"Operacion guardada correctamente con folio {folio}.";
            Input = new CreateOperationInputModel { Database = SelectedDatabase };
        });
    }

    private Task RefreshActiveAsync() => ActiveSection switch
    {
        "Operaciones" => LoadOperationsAsync(),
        "Comisiones" => LoadCommissionsAsync(),
        "Vendedores" => LoadVendorsAsync(),
        "Productos" => LoadProductsAsync(),
        "Catalogos" => LoadCatalogsAsync(),
        "Nueva operacion" => LoadCreateOperationAsync(),
        _ => LoadDashboardAsync()
    };

    private async Task RunAsync(string section, Func<PortalService, Task> action)
    {
        try
        {
            IsBusy = true;
            ActiveSection = section;
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<PortalService>();
            await action(service);
        }
        catch (Exception ex)
        {
            Message = $"No fue posible cargar {section}. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }
}
