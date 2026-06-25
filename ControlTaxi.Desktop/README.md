# Control de Taxi Desktop WPF

Aplicacion Desktop Windows para operar Control de Taxi sin navegador ni servidor web externo.

## Requisitos

- Windows 10/11.
- .NET 9 SDK o runtime Desktop.
- SQL Server local o en red local.
- Bases de datos:
  - `ControlTaxis`
  - `mkt2`
  - `compuamdoPlaza`
  - `joyeriaPlaza`

## Configuracion de conexion

La aplicacion usa `ControlTaxi.Desktop/appsettings.json` como configuracion base y crea una copia local editable en:

```text
%AppData%\ControlTaxi\appsettings.Local.json
```

La contrasena de SQL Server no se deja fija en C# ni se versiona con Git. Configure una de estas opciones:

1. Editar `%AppData%\ControlTaxi\appsettings.Local.json`.
2. Definir variable de entorno:

```powershell
setx CONTROLTAXI_SQL_PASSWORD "SU_CONTRASENA"
```

Ejemplo de cadena local:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=mkt2;User Id=REYNA;Password=SU_CONTRASENA;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True;"
  }
}
```

## Compilar

Desde la raiz del repositorio:

```powershell
dotnet restore "CONTROL TAXI.sln"
dotnet build "CONTROL TAXI.sln" -c Release
```

## Ejecutar

```powershell
dotnet run --project ControlTaxi.Desktop/ControlTaxi.Desktop.csproj
```

Tambien puede abrir `CONTROL TAXI.sln` en Visual Studio y seleccionar `ControlTaxi.Desktop` como proyecto de inicio.

## Operacion sin internet

Por defecto:

```json
"PosSqlMirror": {
  "AutoSyncAppMovilFromApi": false
}
```

Con esta configuracion, la operacion local usa SQL Server local/red local y no bloquea login, captura, consultas, pagos, cortes, catalogos ni reportes por falta de internet.

Si se requiere sincronizacion con app movil/Hostinger, configure `TaxiApi:BaseUrl`, `TaxiApi:SyncToken` y cambie `AutoSyncAppMovilFromApi` a `true` en el archivo local. Si la red falla, los servicios existentes registran advertencias y continuan usando datos locales cuando hay fallback.
