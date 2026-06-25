using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ControlTaxi.Desktop.Infrastructure;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class DesktopSession : ObservableObject
{
    private string _usuario = string.Empty;
    private IReadOnlyCollection<string> _permisos = Array.Empty<string>();
    private bool _isAuthenticated;

    public string Usuario
    {
        get => _usuario;
        private set => SetProperty(ref _usuario, value);
    }

    public IReadOnlyCollection<string> Permisos
    {
        get => _permisos;
        private set => SetProperty(ref _permisos, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set => SetProperty(ref _isAuthenticated, value);
    }

    public void SignIn(string usuario, IReadOnlyCollection<string> permisos)
    {
        Usuario = usuario;
        Permisos = permisos;
        IsAuthenticated = true;
    }

    public void SignOut()
    {
        Usuario = string.Empty;
        Permisos = Array.Empty<string>();
        IsAuthenticated = false;
    }

    public bool CanAccess(string permissionKey) =>
        string.IsNullOrWhiteSpace(permissionKey) ||
        Permisos.Contains(permissionKey, StringComparer.OrdinalIgnoreCase) ||
        Permisos.Contains("Usuarios", StringComparer.OrdinalIgnoreCase);
}

public sealed class DataRowItem(string[] columns, IReadOnlyList<string> values)
{
    public IReadOnlyList<string> Columns { get; } = columns;
    public IReadOnlyList<string> Values { get; } = values;

    public string this[string columnName]
    {
        get
        {
            var index = Array.FindIndex(columns, x => string.Equals(x, columnName, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index < Values.Count ? Values[index] : string.Empty;
        }
    }
}

public static class RowProjection
{
    public static ObservableCollection<object> FromObjects(IEnumerable rows) =>
        new(rows.Cast<object>());

    public static ObservableCollection<object> FromStringRows(IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string>> rows)
    {
        var normalizedColumns = columns.Count == 0
            ? Enumerable.Range(1, rows.FirstOrDefault()?.Count ?? 0).Select(x => $"Columna {x}").ToArray()
            : columns.ToArray();

        return new ObservableCollection<object>(
            rows.Select(x => (object)new DataRowItem(normalizedColumns, x.ToArray())));
    }
}
