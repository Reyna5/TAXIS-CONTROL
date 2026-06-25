using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxi.Desktop.Services;
using ControlTaxiWeb.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class LoginViewModel(
    IServiceScopeFactory scopeFactory,
    DesktopSession session) : ObservableObject
{
    private string _usuario = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public string Usuario
    {
        get => _usuario;
        set => SetProperty(ref _usuario, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task<bool> LoginAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(Usuario))
        {
            ErrorMessage = "Capture el usuario.";
            return false;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            using var scope = scopeFactory.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IPosAuthService>();
            var usuarios = scope.ServiceProvider.GetRequiredService<IPosUsuariosService>();

            if (!await auth.ValidateUserAsync(Usuario.Trim(), password))
            {
                ErrorMessage = "Usuario o contrasena incorrectos.";
                return false;
            }

            var permisos = await usuarios.GetPermissionsAsync(Usuario.Trim());
            session.SignIn(Usuario.Trim(), permisos);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "No fue posible iniciar sesion. Revise SQL Server y la configuracion local. " + ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class MainViewModel : ObservableObject
{
    private readonly DesktopSession _session;
    private readonly ModuleDataService _moduleDataService;
    private readonly SettingsViewModel _settings;
    private readonly IServiceProvider _serviceProvider;
    private NavigationItemViewModel? _selectedNavigationItem;
    private object? _content;

    public MainViewModel(
        DesktopSession session,
        LoginViewModel login,
        ModuleCatalog moduleCatalog,
        ModuleDataService moduleDataService,
        SettingsViewModel settings,
        IServiceProvider serviceProvider)
    {
        _session = session;
        Login = login;
        _moduleDataService = moduleDataService;
        _settings = settings;
        _serviceProvider = serviceProvider;

        NavigationItems = new ObservableCollection<NavigationItemViewModel>(
            moduleCatalog.Modules.Select(x => new NavigationItemViewModel(x)));

        Content = Login;
        NavigateCommand = new RelayCommand(NavigateSelected, () => IsAuthenticated);
        SignOutCommand = new RelayCommand(SignOut);

        _session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DesktopSession.IsAuthenticated))
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsLoginVisible));
                OnPropertyChanged(nameof(IsShellVisible));
            }

            if (e.PropertyName is nameof(DesktopSession.Permisos) or nameof(DesktopSession.IsAuthenticated))
                RefreshPermissions();
        };
    }

    public LoginViewModel Login { get; }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public RelayCommand NavigateCommand { get; }

    public RelayCommand SignOutCommand { get; }

    public bool IsAuthenticated => _session.IsAuthenticated;

    public bool IsLoginVisible => !IsAuthenticated;

    public bool IsShellVisible => IsAuthenticated;

    public string Usuario => _session.Usuario;

    public NavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (SetProperty(ref _selectedNavigationItem, value) && value is not null)
                NavigateSelected();
        }
    }

    public object? Content
    {
        get => _content;
        private set => SetProperty(ref _content, value);
    }

    public void CompleteLogin()
    {
        OnPropertyChanged(nameof(Usuario));
        RefreshPermissions();
        SelectedNavigationItem = NavigationItems.FirstOrDefault(x => x.IsEnabled);
    }

    private void NavigateSelected()
    {
        if (!_session.IsAuthenticated || SelectedNavigationItem is null || !SelectedNavigationItem.IsEnabled)
            return;

        if (SelectedNavigationItem.Definition.Key == "configuracion")
        {
            Content = _settings;
            return;
        }

        if (SelectedNavigationItem.Definition.Key == "ventas")
        {
            Content = _serviceProvider.GetRequiredService<VentasViewModel>();
            return;
        }

        if (SelectedNavigationItem.Definition.Key == "portal")
        {
            Content = _serviceProvider.GetRequiredService<PortalViewModel>();
            return;
        }

        if (SelectedNavigationItem.Definition.Key == "reportes")
        {
            Content = _serviceProvider.GetRequiredService<ReportesViewModel>();
            return;
        }

        Content = new ModuleWorkspaceViewModel(
            SelectedNavigationItem.Definition,
            _moduleDataService,
            _session);
    }

    private void SignOut()
    {
        _session.SignOut();
        Content = Login;
    }

    private void RefreshPermissions()
    {
        foreach (var item in NavigationItems)
            item.IsEnabled = _session.IsAuthenticated && _session.CanAccess(item.Definition.Permission);
    }
}

public sealed class NavigationItemViewModel(ModuleDefinition definition) : ObservableObject
{
    private bool _isEnabled = true;

    public ModuleDefinition Definition { get; } = definition;

    public string Title => Definition.Title;

    public string Icon => Definition.Icon;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

public sealed class ModuleWorkspaceViewModel : ObservableObject
{
    private readonly ModuleDefinition _definition;
    private readonly ModuleDataService _dataService;
    private readonly DesktopSession _session;
    private string _title;
    private string _summary;
    private bool _isBusy;
    private ObservableCollection<object> _rows = [];
    private object? _rawModel;
    private string _message = string.Empty;

    public ModuleWorkspaceViewModel(
        ModuleDefinition definition,
        ModuleDataService dataService,
        DesktopSession session)
    {
        _definition = definition;
        _dataService = dataService;
        _session = session;
        _title = definition.Title;
        _summary = definition.Description;
        Query = new ModuleQuery();
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        PrimaryActionCommand = new AsyncRelayCommand(ExecutePrimaryActionAsync, HasPrimaryAction);
        SecondaryActionCommand = new AsyncRelayCommand(ExecuteSecondaryActionAsync, HasSecondaryAction);
        _ = LoadAsync();
    }

    public ModuleQuery Query { get; }

    public AsyncRelayCommand LoadCommand { get; }

    public AsyncRelayCommand PrimaryActionCommand { get; }

    public AsyncRelayCommand SecondaryActionCommand { get; }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<object> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string PrimaryActionText => _definition.Key switch
    {
        "registro" => "Guardar registro",
        "registro-app" => "Crear registro App",
        "relaciones" => "Guardar relacion",
        "gafetes" => "Guardar gafete",
        "pagos" => "Registrar pago",
        "gastos" => "Guardar gasto",
        "cortes" => "Cerrar corte",
        "comisiones" => "Recalcular",
        "transportes" => "Guardar transporte",
        "guias" => "Guardar guia",
        "taxistas" => "Guardar taxista",
        "usuarios" => "Guardar usuario",
        _ => string.Empty
    };

    public string SecondaryActionText => _definition.Key switch
    {
        "relaciones" => "Pagar dejada",
        "gafetes" => "Registrar regreso",
        "comisiones" => "Pagar comision",
        _ => string.Empty
    };

    public bool HasPrimaryAction() => !string.IsNullOrWhiteSpace(PrimaryActionText);

    public bool HasSecondaryAction() => !string.IsNullOrWhiteSpace(SecondaryActionText);

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            Message = "Cargando datos...";
            var result = await _dataService.LoadAsync(_definition, Query, _session.Usuario);
            Title = result.Title;
            Summary = result.Summary;
            Rows = result.Rows;
            _rawModel = result.RawModel;
            Message = $"Datos actualizados: {DateTime.Now:g}";
        }
        catch (Exception ex)
        {
            Message = "No fue posible cargar el modulo. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecutePrimaryActionAsync()
    {
        var action = _definition.Key switch
        {
            "registro" => "registro-guardar",
            "registro-app" => "registro-app-crear",
            "relaciones" => "relaciones-guardar",
            "gafetes" => "gafetes-guardar",
            "pagos" => "pagos-registrar",
            "gastos" => "gastos-guardar",
            "cortes" => "cortes-cerrar",
            "comisiones" => "comisiones-recalcular",
            "transportes" => "transportes-guardar",
            "guias" => "guias-guardar",
            "taxistas" => "taxistas-guardar",
            "usuarios" => "usuarios-guardar",
            _ => string.Empty
        };
        await ExecuteActionAsync(action);
    }

    private async Task ExecuteSecondaryActionAsync()
    {
        var action = _definition.Key switch
        {
            "relaciones" => "relaciones-pagar-dejada",
            "gafetes" => "gafetes-regreso",
            "comisiones" => "comisiones-pagar",
            _ => string.Empty
        };
        await ExecuteActionAsync(action);
    }

    private async Task ExecuteActionAsync(string actionKey)
    {
        if (string.IsNullOrWhiteSpace(actionKey))
            return;

        try
        {
            IsBusy = true;
            var result = await _dataService.ExecuteActionAsync(actionKey, Query, _session.Usuario);
            Message = result.Message;
            if (result.Success)
                await LoadAsync();
        }
        catch (Exception ex)
        {
            Message = "La operacion fallo. " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class SettingsViewModel(IConfiguration configuration) : ObservableObject
{
    private string _message = string.Empty;

    public string LocalSettingsPath => DesktopSettingsPaths.EnsureLocalSettingsFile();

    public string Server => configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

    public string AppDatabase => configuration["DatabaseNames:App"] ?? string.Empty;

    public string PosDatabase => configuration["DatabaseNames:Pos"] ?? string.Empty;

    public string CompuadmoDatabase => configuration["DatabaseNames:Compuadmo"] ?? string.Empty;

    public string JoyeriaDatabase => configuration["DatabaseNames:Joyeria"] ?? string.Empty;

    public string ExternalSyncStatus =>
        configuration.GetValue<bool>("PosSqlMirror:AutoSyncAppMovilFromApi")
            ? "Sincronizacion externa habilitada"
            : "Sincronizacion externa deshabilitada; operacion local disponible sin internet";

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public RelayCommand OpenLocalSettingsCommand => new(OpenLocalSettings);

    private void OpenLocalSettings()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = LocalSettingsPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            Message = "Archivo de configuracion local abierto.";
        }
        catch (Exception ex)
        {
            Message = "No fue posible abrir la configuracion local. " + ex.Message;
        }
    }
}
