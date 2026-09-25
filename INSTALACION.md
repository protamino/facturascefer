# FacturasCefer — Instalación

## Contenido del paquete

`2026-09-25-FacturasCefer-deploy.zip`:

| Fichero | Qué es |
|---|---|
| `FacturasCefer.exe` | La aplicación (incluye .NET; no hay que instalar nada más) |
| `appsettings.json` | Configuración: conexión a BD, carpeta de red, clave de Claude. **Contiene contraseñas: no compartir fuera de CEFER** |

## Instalar en un PC

1. Crear la carpeta `C:\FacturasCefer\` (o cualquier carpeta local; **no** dentro de Dropbox ni en red).
2. Copiar dentro `FacturasCefer.exe` y `appsettings.json` (siempre juntos, en la misma carpeta).
3. Crear un acceso directo a `FacturasCefer.exe` en el escritorio.
4. Abrir y entrar con el usuario y contraseña de SIC.

La primera vez tarda unos segundos más en arrancar (descomprime componentes internos).

## Requisitos del PC

- Windows 10 u 11 de 64 bits.
- Red hasta **SQL-01 (192.168.0.15)**, puerto 1433.
- **Escritura** en `\\192.168.0.10\Cefer\FacturasProveedores` con el usuario de Windows que use la app.
- Salida a internet hacia **api.anthropic.com** (HTTPS, 443) para leer las facturas con IA.
- **Microsoft Edge WebView2 Runtime** para ver el PDF dentro de la app (viene de serie en Windows 10/11 actualizados).
  Si falta, la app avisa y se puede usar «Abrir en visor externo»; se instala desde
  <https://developer.microsoft.com/microsoft-edge/webview2/> (Evergreen Standalone Installer).

## Actualizar a una versión nueva

Cerrar la app y sustituir solo `FacturasCefer.exe`. El `appsettings.json` se conserva salvo que cambie la configuración.

## Configuración (`appsettings.json`)

| Clave | Valor |
|---|---|
| `Facturas.ConnectionString` | BD `FACTURASCEFER` en SQL-01 |
| `Dmstra.ConnectionString` | BD `DMSTRA` (login de usuarios de SIC) |
| `Repositorio.RutaUnc` | `\\192.168.0.10\Cefer\FacturasProveedores` |
| `CifPropio` | CIF de CEFER (`B60508918`), para que la IA no lo confunda con el del proveedor |
| `Claude.ApiKey` | Clave de la API de Claude (console.anthropic.com) |
| `Claude.Model` | `claude-opus-5` |

En JSON las barras invertidas van dobles: `"\\\\192.168.0.10\\Cefer\\FacturasProveedores"`.

## Si algo falla

Los errores técnicos se guardan en `%TEMP%\FacturasCefer\` (`app-error.log`, `login-error.log`, `ia-error.log`,
`webview2-error.log`). Enviar ese fichero junto con el mensaje que muestra la app.

## Generar el paquete (desarrollo)

```bash
dotnet publish src/FacturasCefer/FacturasCefer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish
```

Después borrar los `*.xml` de `publish\` y comprimir `FacturasCefer.exe` + `appsettings.json`.
