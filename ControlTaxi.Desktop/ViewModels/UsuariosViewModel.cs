using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Models.PosTriton;
using Microsoft.Extensions.DependencyInjection;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class UsuariosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DesktopSession _session;
    private PosUsuarioRowViewModel? _selectedUsuario;
    private string _message = string.Empty;

    public UsuariosViewModel(IServiceScopeFactory scopeFactory, DesktopSession session)
    {
        _scopeFactory = scopeFactory;
        _session = session;
        Permisos = new ObservableCollection<PermisoCheckViewModel>(
            PosModuloPermisoViewModel.Catalogo().Select(x => new PermisoCheckViewModel(x.Clave, x.Nombre)));
        ConsultarCommand = new AsyncRelayCommand(ConsultarAsync);
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
        NuevoCommand = new RelayCommand(Nuevo);
        _ = ConsultarAsync();
    }

    public ObservableCollection<PosUsuarioRowViewModel> Usuarios { get; } = [];
    public ObservableCollection<PermisoCheckViewModel> Permisos { get; }

    public AsyncRelayCommand ConsultarCommand { get; }
    public AsyncRelayCommand GuardarCommand { get; }
    public RelayCommand NuevoCommand { get; }

    public string UsuarioEdicion { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Rol { get; set; } = "Cajero";
    public string Estatus { get; set; } = "Activo";

    public PosUsuarioRowViewModel? SelectedUsuario
    {
        get => _selectedUsuario;
        set
        {
            if (SetProperty(ref _selectedUsuario, value) && value is not null)
                ApplyUsuario(value);
        }
    }

    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    private async Task ConsultarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosUsuariosService>();
        var model = await service.TryGetAsync(UsuarioEdicion) ?? new PosUsuariosViewModel();
        Usuarios.Clear();
        foreach (var usuario in model.Usuarios)
            Usuarios.Add(usuario);
        Message = $"{Usuarios.Count} usuarios cargados.";
    }

    private async Task GuardarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosUsuariosService>();
        var permisos = Permisos.Where(x => x.IsChecked).Select(x => x.Clave).ToList();
        var ok = await service.SaveAsync(UsuarioEdicion, Password, Rol, Estatus, permisos, _session.Usuario);
        Message = ok ? "Usuario guardado." : "No se pudo guardar usuario.";
        if (ok)
        {
            if (string.Equals(_session.Usuario, UsuarioEdicion, StringComparison.OrdinalIgnoreCase))
            {
                var refreshed = await service.GetPermissionsAsync(UsuarioEdicion);
                _session.SignIn(_session.Usuario, refreshed);
            }
            await ConsultarAsync();
        }
    }

    private void ApplyUsuario(PosUsuarioRowViewModel usuario)
    {
        UsuarioEdicion = usuario.Usuario;
        Password = string.Empty;
        Rol = usuario.Rol;
        Estatus = usuario.Estatus;
        var set = usuario.Permisos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var permiso in Permisos)
            permiso.IsChecked = set.Contains(permiso.Clave);
        NotifyForm();
    }

    private void Nuevo()
    {
        UsuarioEdicion = string.Empty;
        Password = string.Empty;
        Rol = "Cajero";
        Estatus = "Activo";
        foreach (var permiso in Permisos)
            permiso.IsChecked = false;
        NotifyForm();
    }

    private void NotifyForm()
    {
        OnPropertyChanged(nameof(UsuarioEdicion));
        OnPropertyChanged(nameof(Password));
        OnPropertyChanged(nameof(Rol));
        OnPropertyChanged(nameof(Estatus));
    }
}

public sealed class PermisoCheckViewModel(string clave, string nombre) : ObservableObject
{
    private bool _isChecked;
    public string Clave { get; } = clave;
    public string Nombre { get; } = nombre;
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
}
