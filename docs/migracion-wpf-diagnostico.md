# Diagnostico de migracion a WPF Desktop

## Estructura actual

- Solucion: `CONTROL TAXI.sln`.
- Proyecto web actual: `ControlTaxiWeb.csproj` (`Microsoft.NET.Sdk.Web`, `net9.0`).
- Entrada ASP.NET Core MVC: `Program.cs`, ruta principal `Pos/Index`.
- Capas detectadas:
  - `Controllers/`: controladores MVC (`PosController`, `PortalController`).
  - `Services/`: servicios de negocio y acceso a SQL Server.
  - `Services/PosModules/`: fachadas por modulo para POS.
  - `Interfaces/`: contratos de servicios.
  - `Data/`: entidades y DbContexts EF Core.
  - `Models/PosTriton/`: view models reutilizables por modulo.
  - `Views/`: vistas Razor de la version web.
  - `wwwroot/`: assets web.

## Modulos detectados

- Login/autenticacion.
- Dashboard POS.
- Usuarios, roles y permisos.
- Registro diario.
- Registro App.
- Relaciones ticket-taxista.
- Gafetes.
- Pagos.
- Ventas.
- Comisiones.
- Gastos.
- Cortes.
- Catalogos: transportes, guias y taxistas.
- Reportes: operaciones, dejadas, comisiones, cuadre/concentrado.
- Auditoria de movimientos.
- Sincronizacion opcional con app movil/API Hostinger.
- Portal secundario para bases Compuadmo/Joyeria.

## Servicios reutilizables

La logica de negocio principal esta concentrada en `PosSqlMirrorService` y sus archivos partial:

- `PosSqlMirrorService.Auth.cs`
- `PosSqlMirrorService.Registro.cs`
- `PosSqlMirrorService.AppMovilSync.cs`
- `PosSqlMirrorService.Relaciones.cs`
- `PosSqlMirrorService.Gafetes.cs`
- `PosSqlMirrorService.Pagos.cs`
- `PosSqlMirrorService.Ventas*.cs`
- `PosSqlMirrorService.Comisiones.cs`
- `PosSqlMirrorService.Gastos.cs`
- `PosSqlMirrorService.Cortes.cs`
- `PosSqlMirrorService.Catalogos.cs`
- `PosSqlMirrorService.Auditoria.cs`

Estos servicios no dependen de `HttpContext`, Razor ni MVC, por lo que se pueden reutilizar desde WPF mediante inyeccion de dependencias.

## Bases de datos detectadas

- `ControlTaxis`: usuarios, permisos, ventas, cortes, gafetes y datos Triton.
- `mkt`: operacion POS legacy, dejadas, app movil, relaciones, auditoria.
- `compuadmo`: remisiones, productos y comisiones de tienda.
- `joyeria`: remisiones, productos y comisiones de joyeria.

La version web tenia una cadena remota con credenciales en `appsettings.json`. Para Desktop se debe usar configuracion local editable y no dejar contrasenas fijas dentro del codigo fuente.

## Dependencias externas

- SQL Server local o red local.
- API Hostinger configurada en `TaxiApi`.
- Scripts PowerShell/CMD de sincronizacion app movil.

La sincronizacion Hostinger debe quedar opcional. La operacion local no debe bloquearse si no hay internet.

## Archivos basura o de publicacion detectados

- `PUBLICAR_CONTROL_TAXI/`
- `CONTROL_TAXI_SUBIR_SERVIDOR_ACTUALIZADO/`
- Copias duplicadas de `api-php-hostinger/` dentro de publicaciones.
- Archivos de arranque de servidor web/IIS en publicaciones.
- `*.deps.json`, `*.runtimeconfig.json`, `*.staticwebassets.endpoints.json` dentro de publicaciones.

Se conserva una sola copia canonica de scripts operativos en `api-php-hostinger/` y `tools/`.

## Riesgos de migracion

- `PosController` contiene composicion de UI, filtros de permiso y exportaciones que deben moverse a ViewModels/servicios Desktop.
- Los reportes Excel/PDF estan mezclados en el controlador web.
- La autenticacion web usa session; WPF necesita estado local de usuario/permisos.
- La API Hostinger puede fallar sin internet; se debe manejar como funcionalidad opcional.
- Las cuatro bases SQL deben mantenerse configurables para instalaciones locales.
- WPF requiere conversion completa de vistas Razor a XAML.

## Plan tecnico

1. Limpiar publicaciones y artefactos generados.
2. Crear proyecto WPF `ControlTaxi.Desktop` dentro de la solucion.
3. Reutilizar `Data`, `Models`, `Interfaces` y `Services` existentes mediante compilacion enlazada.
4. Centralizar configuracion Desktop en archivos JSON editables y no codificar contrasenas en C#.
5. Registrar DI con `Microsoft.Extensions.Hosting`.
6. Implementar MVVM para login, dashboard, modulos operativos, catalogos, usuarios, reportes y configuracion.
7. Mantener sincronizacion externa como opcional/configurable.
8. Validar compilacion por etapa y dejar commits claros.
