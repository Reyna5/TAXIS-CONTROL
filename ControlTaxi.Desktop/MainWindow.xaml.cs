using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ControlTaxi.Desktop.ViewModels;

namespace ControlTaxi.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await TryLoginAsync();
    }

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await TryLoginAsync();
    }

    private async Task TryLoginAsync()
    {
        if (DataContext is not MainViewModel main)
            return;

        if (await main.Login.LoginAsync(PasswordBox.Password))
        {
            PasswordBox.Clear();
            main.CompleteLogin();
        }
    }
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class EnabledOpacityConverter : IValueConverter
{
    public static EnabledOpacityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool enabled && enabled ? 1.0 : 0.38;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
