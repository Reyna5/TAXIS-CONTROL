using System.Collections.ObjectModel;
using ControlTaxi.Desktop.Infrastructure;
using ControlTaxi.Desktop.Services;

namespace ControlTaxi.Desktop.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    public DashboardViewModel(IEnumerable<NavigationItemViewModel> navigationItems, Action<string> navigate)
    {
        Modules = new ObservableCollection<DashboardTileViewModel>(
            navigationItems
                .Where(x => x.Definition.Key is not "dashboard" and not "configuracion" && x.IsEnabled)
                .Select(x => new DashboardTileViewModel(x.Definition, navigate)));
    }

    public ObservableCollection<DashboardTileViewModel> Modules { get; }
}

public sealed class DashboardTileViewModel
{
    public DashboardTileViewModel(ModuleDefinition definition, Action<string> navigate)
    {
        Key = definition.Key;
        Title = definition.Title.ToUpperInvariant();
        Description = definition.Description;
        Icon = definition.Icon;
        OpenCommand = new RelayCommand(() => navigate(Key));
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public string Icon { get; }
    public RelayCommand OpenCommand { get; }
}
