using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using ControlTaxi.Desktop.Services;
using ControlTaxi.Desktop.ViewModels;
using ControlTaxiWeb.Data;
using ControlTaxiWeb.Interfaces;
using ControlTaxiWeb.Services;
using ControlTaxiWeb.Services.PosModules;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlTaxi.Desktop.Infrastructure;

public static class DesktopHost
{
    public static IHost Create()
    {
        var localSettingsPath = DesktopSettingsPaths.EnsureLocalSettingsFile();

        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.Sources.Clear();
                configuration
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile(localSettingsPath, optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables("CONTROLTAXI_");
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                services.AddMemoryCache();
                services.AddSingleton<DesktopSchemaInitializer>();
                services.AddSingleton<DesktopSession>();
                services.AddSingleton<ModuleCatalog>();
                services.AddSingleton<ModuleDataService>();
                services.AddSingleton<DesktopExportService>();
                services.AddSingleton<TicketPrintService>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<LoginViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<VentasViewModel>();
                services.AddTransient<PortalViewModel>();
                services.AddTransient<ReportesViewModel>();
                services.AddTransient<RelacionesViewModel>();
                services.AddTransient<GafetesViewModel>();
                services.AddTransient<ComisionesViewModel>();
                services.AddTransient<CortesViewModel>();
                services.AddTransient<CatalogosViewModel>();
                services.AddTransient<UsuariosViewModel>();
                services.AddTransient<SyncAuditoriaViewModel>();
                services.AddTransient<PagosViewModel>();
                services.AddTransient<GastosViewModel>();

                services.AddDbContextFactory<CompuadmoPlazaContext>(options =>
                    options.UseSqlServer(DatabaseConnectionBuilder.Build(configuration, "DatabaseNames:Compuadmo")));
                services.AddScoped(sp =>
                    sp.GetRequiredService<IDbContextFactory<CompuadmoPlazaContext>>().CreateDbContext());
                services.AddDbContextFactory<JoyeriaPlazaContext>(options =>
                    options.UseSqlServer(DatabaseConnectionBuilder.Build(configuration, "DatabaseNames:Joyeria")));
                services.AddDbContext<PosDbContext>(options =>
                    options.UseSqlServer(DatabaseConnectionBuilder.Build(configuration, "DatabaseNames:Pos")));
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(DatabaseConnectionBuilder.Build(configuration, "DatabaseNames:App")));

                services.AddScoped<PortalService>();
                services.AddHttpClient<IAppTaxiApiClient, AppTaxiApiClient>((sp, client) =>
                {
                    var baseUrl = sp.GetRequiredService<IConfiguration>()["TaxiApi:BaseUrl"];
                    client.BaseAddress = string.IsNullOrWhiteSpace(baseUrl)
                        ? new Uri("http://127.0.0.1/")
                        : new Uri(baseUrl.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromSeconds(4);
                });

                services.AddScoped<IPosSqlMirrorService, PosSqlMirrorService>();
                services.AddScoped<IPosAuthService, PosAuthService>();
                services.AddScoped<IPosDashboardService, PosDashboardService>();
                services.AddScoped<IPosRegistroService, PosRegistroService>();
                services.AddScoped<IPosVentasService, PosVentasService>();
                services.AddScoped<IPosPagosService, PosPagosService>();
                services.AddScoped<IPosGastosService, PosGastosService>();
                services.AddScoped<IPosCortesService, PosCortesService>();
                services.AddScoped<IPosComisionesService, PosComisionesService>();
                services.AddScoped<IPosCatalogosService, PosCatalogosService>();
                services.AddScoped<IPosGafetesService, PosGafetesService>();
                services.AddScoped<IPosUsuariosService, PosUsuariosService>();
                services.AddScoped<IPosRelacionesService, PosRelacionesService>();
            })
            .Build();
    }
}

public static class DesktopSettingsPaths
{
    public static string EnsureLocalSettingsFile()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ControlTaxi");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, "appsettings.Local.json");
        if (File.Exists(path))
            return path;

        var samplePath = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.sample.json");
        if (File.Exists(samplePath))
        {
            File.Copy(samplePath, path, overwrite: false);
        }
        else
        {
            File.WriteAllText(path, """
            {
              "ConnectionStrings": {
                "DefaultConnection": "Server=.\\SQLEXPRESS;Database=mkt2;User Id=REYNA;Password=;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True;"
              },
              "PosSqlMirror": {
                "AutoSyncAppMovilFromApi": false
              }
            }
            """);
        }

        return path;
    }
}

public static class DatabaseConnectionBuilder
{
    public static string Build(IConfiguration configuration, string databaseKey)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(defaultConnection))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection no esta configurado.");

        var databaseName = configuration[databaseKey];
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException($"{databaseKey} no esta configurado.");

        var builder = new SqlConnectionStringBuilder(defaultConnection)
        {
            InitialCatalog = databaseName
        };

        var passwordFromEnvironment = Environment.GetEnvironmentVariable("CONTROLTAXI_SQL_PASSWORD");
        if (!string.IsNullOrWhiteSpace(passwordFromEnvironment))
            builder.Password = passwordFromEnvironment;

        return builder.ConnectionString;
    }
}

public sealed class DesktopSchemaInitializer(
    IServiceProvider services,
    ILogger<DesktopSchemaInitializer> logger)
{
    public async Task TryInitializeAsync()
    {
        try
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Data", "PosTritonSchema.sql");
            if (!File.Exists(scriptPath))
                return;

            var script = await File.ReadAllTextAsync(scriptPath);
            var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            foreach (var batch in batches)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            logger.LogWarning(ex, "SQL Server no esta disponible al iniciar. La aplicacion Desktop abrira para permitir configurar la conexion.");
        }
    }
}
