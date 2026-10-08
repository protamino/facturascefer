# Importación automática de facturas desde el correo

```
Outlook (buzón de facturas)
   │  n8n (n8n.institutocefer.com), cada minuto
   ▼
Claude Haiku clasifica el correo ──► dudoso ──────────────► Outlook: Facturas/Revisar
   │                             └─► reclamación / otro ──► se queda en la bandeja
   ▼ factura_nueva
Google Drive  CEFER/Facturas  (PDF + remitente/asunto)      Outlook: Facturas/Procesadas
   │  servicio FacturasCefer Importador (192.168.0.17), cada 5 min
   ▼
Misma lógica que «Subir facturas» de la app ──► FACTURASCEFER (+ PDF en \\192.168.0.10\...)
   │
   ▼  PDF de Drive movido a  Procesadas / Duplicadas / SinFactura / Error
```

Reglas del importador (sin intervención humana):

| Situación | Qué hace |
|---|---|
| Proveedor nuevo (CIF desconocido) | Lo da de alta con los datos de la factura y la marca **⚑ pendiente de revisar** |
| Factura duplicada (proveedor + nº) | No la registra; PDF a `Duplicadas` |
| IBAN distinto al del proveedor | **No** cambia el proveedor; registra la factura con ⚑ «posible fraude» |
| No cuadra, datos dudosos, sin cuenta de gasto, proveedor de baja, forma de pago distinta | La registra con ⚑ y el motivo |
| Falta CIF, nº, fecha o total | No la registra; PDF a `Error` (subirla a mano desde la app) |
| El PDF no contiene facturas | PDF a `SinFactura` |
| Pago con tarjeta | Se registra como Pagada (igual que en la app) |

En la app: **Facturas** → «⚑ Solo por revisar» / Origen «Correo»; al abrir la ficha se ve el motivo y el botón **Marcar como revisada** (validar la factura también la quita).
**Proveedores** → «⚑ Solo altas automáticas por revisar»; la marca se quita al guardar la ficha.

---

## 1. Base de datos

Ejecutar **`sql/2026-10-08-importador.sql`** en SQL-01 **antes** de instalar la app 1.6.0 (la app ya usa las columnas nuevas).

## 2. Usuario de DMSTRA para las altas automáticas

Crear en DMSTRA un usuario (p. ej. «Facturas automáticas») y apuntar su `idUsuario` → `Importador.IdUsuario`. Es el que aparece como autor en el historial.

## 3. Google Drive

1. En la carpeta **CEFER/Facturas** de Drive, copiar su id de la URL (`https://drive.google.com/drive/folders/<ID>`) → `Importador.CarpetaEntradaId`.
2. Google Cloud Console → proyecto (nuevo o existente) → **APIs y servicios → Habilitar API → Google Drive API**.
3. **IAM → Cuentas de servicio → Crear** (p. ej. `facturascefer-importador`). Sin roles.
4. En la cuenta de servicio → **Claves → Añadir clave → JSON**. Guardar el fichero como `google-service-account.json` junto al exe del importador. **No subirlo a git ni enviarlo por correo.**
5. Compartir la carpeta CEFER/Facturas con el correo de la cuenta de servicio (`...@...iam.gserviceaccount.com`) como **Editor**.

Las subcarpetas `Procesadas`, `Duplicadas`, `SinFactura` y `Error` las crea el importador.

## 4. Outlook

1. En el buzón de facturas crear las carpetas **Facturas → Procesadas** y **Facturas → Revisar**.
2. Azure Portal → **Microsoft Entra ID → Registros de aplicaciones → Nuevo registro** (p. ej. «n8n Facturas»):
   - Cuentas: solo este directorio.
   - URI de redirección (Web): `https://n8n.institutocefer.com/rest/oauth2-credential/callback`
   - **Permisos de API** → Microsoft Graph → delegados: `Mail.ReadWrite`, `offline_access`, `User.Read` (y `Mail.ReadWrite.Shared` si es un buzón compartido) → *Conceder consentimiento de administrador*.
   - **Certificados y secretos** → nuevo secreto de cliente (anotar la caducidad: al caducar el workflow deja de leer correo).

## 5. n8n

1. **Workflows → Import from file** → `n8n/2026-10-08-n8n-facturas-correo.json`.
2. Credenciales:
   - **Microsoft Outlook OAuth2 API**: Client ID y secreto de la app del paso 4 → *Connect* con la cuenta del buzón. Si es buzón compartido, activar *Use Shared Mailbox* e indicar la dirección.
   - **Anthropic** (nodo «Clasificar con Claude», autenticación *Predefined Credential Type → Anthropic*): la credencial de Claude que ya existe en n8n.
   - **Google Drive OAuth2 API** (nodos «Subir a Drive» y «Guardar remitente y asunto»): con un usuario de Google que tenga acceso de edición a CEFER/Facturas. *(No usar aquí la cuenta de servicio: no tiene espacio propio en Drive y la subida falla.)*
3. Revisar los nodos con nota:
   - «Correo nuevo»: limitar a la bandeja de entrada del buzón de facturas.
   - «Subir a Drive»: id de la carpeta CEFER/Facturas.
   - «Mover a Facturas/Procesadas» y «Mover a Facturas/Revisar»: elegir la carpeta.
4. Probar con un correo de prueba (*Execute workflow*) y después **activar** el workflow.
5. Recomendado: *Settings → Error workflow* con un workflow que avise por correo si falla.

## 6. Servicio en 192.168.0.17

1. Copiar el contenido de `FacturasCefer.Importador-X.Y.Z.zip` a `C:\FacturasCefer.Importador\`.
2. Copiar `appsettings.example.json` → `appsettings.json` y rellenar (mismas cadenas de conexión, ruta UNC y API key que la app, más la sección `Importador`). Dejar `"Simular": true` para la primera prueba.
3. Copiar `google-service-account.json` a la misma carpeta.
4. Prueba en consola (PowerShell en esa carpeta):
   ```
   .\FacturasCefer.Importador.exe --probar C:\ruta\una-factura.pdf
   .\FacturasCefer.Importador.exe --una-vez --simular
   ```
   `--probar` analiza un PDF local; `--una-vez --simular` lee Drive y muestra qué haría. Ninguno guarda nada.
5. Si todo es correcto: `"Simular": false` e `IdUsuario` relleno.
6. Instalar el servicio (PowerShell **como administrador**):
   ```
   .\instalar-servicio.ps1 -Cuenta 'DOMINIO\usuario'
   ```
   La cuenta debe poder **escribir** en `\\192.168.0.10\Cefer\FacturasProveedores`. Si el servicio no arranca con el error 1069, abrir `services.msc` → *FacturasCefer Importador* → *Iniciar sesión*, volver a escribir la contraseña (concede el derecho «Iniciar sesión como servicio») y arrancarlo.

Requisitos de red del servidor: SQL-01 (1433), la carpeta UNC, `api.anthropic.com` y `www.googleapis.com` / `oauth2.googleapis.com` (443).

## 7. Operación

- **Log**: `C:\FacturasCefer.Importador\logs\importador-AAAA-MM-DD.log` (un fichero por día).
- **Registro** de cada PDF en la tabla `FacturaImportacion` (resultado y mensaje); un PDF ya registrado no se vuelve a procesar.
- Si un PDF da error *técnico* (red, BD caída…) se queda en CEFER/Facturas y se reintenta en la siguiente pasada.
- PDF en `Error`: subirlo a mano desde la app (pestaña «Subir facturas»).
- Para reprocesar un PDF: borrar su fila de `FacturaImportacion` y devolverlo a CEFER/Facturas.
- Actualizar: `Stop-Service FacturasCefer.Importador`, reemplazar los ficheros (conservar `appsettings.json`, `google-service-account.json` y `logs`), `Start-Service FacturasCefer.Importador`.

> Privacidad: el correo y los PDF se envían a Claude (Anthropic) para clasificarlos y extraer los datos. Las facturas pueden contener datos de pacientes: validarlo con el DPD (ver prd.md).
