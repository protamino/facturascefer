# FacturasCefer — Instalación

## Versión

La versión se ve en la pantalla de acceso, en la barra superior de la ventana principal y en
Propiedades → Detalles del `.exe`. Se define en `src/FacturasCefer/FacturasCefer.csproj` (`<Version>`).

| Versión | Fecha | Cambios |
|---|---|---|
| 1.5.0 | 2026-10-02 | Desglose de IVA por factura (varios tipos, recargo, cuenta por línea), exportación a a3ASESOR | con, cuenta contable opcional al subir y filtro de proveedores sin cuenta — requiere `sql/2026-10-02-exportacion-a3.sql` |\| con — requiere `sql/2026-10-02-exportacion-a3.sql` |
| 1.4.0 | 2026-10-02 | Número de versión visible en la app |
| 1.3.0 | 2026-10-02 | Cuentas contables (catálogo, cuenta por defecto del proveedor, cuenta por factura) — requiere `sql/2026-10-02-cuentas-contables.sql` |
| 1.2.0 | 2026-09-25 | Facturas pagadas con tarjeta — requiere `sql/2026-09-25-pago-tarjeta.sql` |
| 1.1.0 | 2026-09-25 | Forma de pago: transferencia / domiciliación — requiere `sql/2026-09-25-forma-pago.sql` |
| 1.0.0 | 2026-09-25 | Proveedores, subida con IA, listado y estados |

Regla: subir el segundo número con funciones nuevas y el tercero con correcciones. Si una versión requiere
un script SQL, ejecutarlo en SQL-01 **antes** de repartir el exe.

## Contenido del paquete

`AAAA-MM-DD-FacturasCefer-X.Y.Z.zip` (p. ej. `2026-10-02-FacturasCefer-1.4.0.zip`):

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

## Exportación a a3ASESOR | con

Pestaña **Facturas** → botón **«⇪ Exportaciones ▸ a3ASESOR | con…»**. Genera un CSV (UTF-8 con BOM, `;`, coma decimal)
para el Importador de Datos de A3. La configuración (columnas, formato, equivalencias) está en
`a3-exportacion.json` junto al exe; la plantilla comentada `a3-exportacion.example.json` se instala con el exe.
Si no existe `a3-exportacion.json` se usan los valores por defecto de la plantilla.

## Si algo falla

Los errores técnicos se guardan en `%TEMP%\FacturasCefer\` (`app-error.log`, `login-error.log`, `ia-error.log`,
`webview2-error.log`). Enviar ese fichero junto con el mensaje que muestra la app.

## Generar el paquete (desarrollo)

```bash
dotnet publish src/FacturasCefer/FacturasCefer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish
```

Después borrar los `*.xml` de `publish\` y comprimir `FacturasCefer.exe` + `appsettings.json` en
`AAAA-MM-DD-FacturasCefer-X.Y.Z.zip`. Antes de publicar, subir `<Version>` en el `.csproj` y añadir la fila al historial.
