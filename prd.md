# FACTURASCEFER — PRD

*Versión 0.6 · 2026-09-25 · Autor: Fernando Marina*

## 1. Objetivo

App de escritorio Windows para registrar las **facturas de proveedores** de CEFER:

- Se sube el PDF de la factura (arrastrar, añadir o pegar). Un PDF puede traer **varias facturas**.
- La IA extrae los datos (proveedor, importes, IBAN…).
- El usuario revisa, corrige y confirma → se crea el registro en `FacturaProveedores`.
- Si el proveedor no existe en `Proveedor`, se da de alta desde la misma pantalla.
- Listado de facturas con su estado (Recibida, Validada, Pagada, Rechazada/Anulada).

**Fuera de alcance (v1):** remesas SEPA, exportación a Excel, integración con contabilidad, facturas emitidas.

## 2. Stack técnico

Mismo patrón que DOCUCEFER:

| Pieza | Elección |
|---|---|
| App | C# .NET WPF (`net7.0-windows` o superior) |
| Distribución | `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` → un único `.exe` |
| BD | SQL Server SQL-01 (192.168.0.15), BD nueva **`FACTURASCEFER`**, login `programacion`, `Encrypt=false` |
| Acceso a datos | `Microsoft.Data.SqlClient` **5.2.2** (la 7.x falla en net7) |
| PDF | PdfSharp (nº de páginas, **separar páginas** por factura) + visor embebido (WebView2) |
| IA | **Claude API** (Anthropic): el PDF se envía como bloque `document` (base64), respuesta en JSON estructurado |
| Config | `appsettings.json` (gitignored) + plantilla `appsettings.example.json` |

## 3. Usuarios y acceso

- Login usuario/contraseña contra **`DMSTRA.dbo.Usuarios`** (`NombreUser` + `Pwd` cifrada AES "Serono"). Reutilizar `SeronoCrypto` y `AuthService` de DOCUCEFER.
- El `idUsuario` se guarda en toda acción auditable: registro de factura, alta de proveedor, cambio de estado.
- Sin roles: cualquier usuario autenticado puede hacerlo todo.

## 4. Alta de factura

### 4.1 Caja de subida
- Zona grande "Arrastra aquí las facturas" que acepta:
  - **Arrastrar** ficheros desde el Explorador / Outlook.
  - Botón **Añadir…** (diálogo de apertura, multiselección).
  - **Ctrl+V** con ficheros copiados.
- Solo PDF. Otros formatos → mensaje de error.
- Varios PDFs a la vez → cola; se procesan y revisan de uno en uno.

### 4.2 Extracción con IA
Claude devuelve un JSON con una **lista de facturas** encontradas en el PDF. Por cada una:

| Campo | Notas |
|---|---|
| Páginas | Página inicial y final dentro del PDF |
| Razón social del proveedor | |
| CIF/NIF del proveedor | Clave principal para identificar al proveedor. No confundir con el CIF de CEFER (`CifPropio` en config, se indica a Claude) |
| Dirección del proveedor | Para prellenar el alta |
| Forma de pago | Transferencia / domiciliación / tarjeta / otra |
| Tarjeta | Si se pagó con tarjeta: marca y últimos 4 dígitos (nunca el número completo) |
| IBAN | Normalizado sin espacios. En domiciliadas es la **cuenta de cargo de CEFER**, no la del proveedor |
| Nº de factura | |
| Fecha de factura | |
| Fecha de vencimiento | Si no aparece, vacía |
| Base imponible | |
| % IVA y cuota IVA | Si hay varios tipos, se suman (v1) |
| % IRPF y cuota IRPF | Opcional |
| Total | |
| Concepto | Resumen breve de lo facturado (p. ej. "Servicios GMA. Paciente …") |

- Cada campo lleva indicador de **no encontrado / baja confianza**, que se resalta en la revisión.
- Se guarda el JSON bruto de la respuesta en la factura (trazabilidad).
- Si falla la API (red, límite) → mensaje y opción de reintentar o rellenar a mano.

### 4.3 Varias facturas en un PDF
- Si la IA detecta más de una factura, la revisión muestra "Factura 1 de N" y se revisa cada una por separado.
- El usuario puede corregir el rango de páginas de cada factura, unir dos detectadas por error o descartar alguna.
- Al guardar, cada factura se **separa en su propio PDF** (PdfSharp) con sus páginas. El PDF original también se conserva.

### 4.4 Pantalla de revisión
- Izquierda: **visor del PDF** (se sitúa en las páginas de la factura en revisión). Derecha: **formulario editable** con los datos extraídos.
- Botones: **Guardar**, **Descartar**, **Siguiente** (siguiente factura del PDF o siguiente PDF de la cola).

### 4.4 bis Forma de pago
- Cada proveedor tiene **forma de pago**: **Transferencia** (CEFER paga al IBAN del proveedor) o **Domiciliación** (el proveedor gira un recibo a la cuenta de CEFER).
- En la revisión se toma la del proveedor (o la que detecta la IA si es nuevo); si la factura indica otra, se avisa.
- En domiciliadas el IBAN de la factura se guarda como **cuenta de cargo** y **no** se compara con el del proveedor (no hay alerta antifraude) ni se copia a su ficha al darlo de alta.
- Las domiciliadas siguen el mismo flujo de estados: se marcan Pagadas a mano al comprobar el cargo (filtro por forma de pago en el listado).
- **Tarjeta**: la factura ya está cobrada. Se indica la tarjeta (marca + últimos 4 dígitos; sugerencias: la habitual del proveedor y las ya usadas) y la fecha de pago (por defecto la de la factura), y se registra **directamente como Pagada**. «Deshacer» la devuelve a Recibida por si se registró por error. 🔒 Nunca se guarda un número de tarjeta completo: la app lo recorta a los últimos 4 dígitos.

### 4.5 Emparejado de proveedor

1. Buscar por **CIF** en `Proveedor`.
2. Si no hay CIF, buscar por razón social (coincidencia aproximada) y proponer candidatos.
3. **No existe** → aviso "Proveedor nuevo" y diálogo de **alta prellenado** (razón social, CIF, dirección, IBAN). Al guardar el proveedor, se vincula a la factura.

### 4.6 Validaciones
| Regla | Comportamiento |
|---|---|
| **IBAN de la factura ≠ IBAN del proveedor** | **Alerta destacada** (posible fraude por cambio de cuenta). El usuario elige: mantener el IBAN del proveedor, o actualizarlo (queda registrado quién y cuándo) |
| Duplicado (mismo proveedor + nº factura) | Bloquea el guardado |
| Base + IVA − IRPF ≠ Total (tolerancia 0,02 €) | Aviso, permite guardar |
| Formato de CIF/NIF e IBAN (dígitos de control) | Aviso |
| Campos obligatorios: proveedor, nº factura, fecha, total | Bloquea |

### 4.7 Guardado
1. Guardar en `\\192.168.0.10\Cefer\FacturasProveedores\{año}\` (ruta configurable):
   - el PDF de la factura (sus páginas) como `{guid}.pdf`;
   - el PDF original subido como `originales\{guidOriginal}.pdf` (una sola vez aunque traiga varias facturas).
2. INSERT en `FacturaProveedores` con estado **Recibida** + fila en el histórico.
3. Si el INSERT falla → se borran los ficheros copiados (rollback).

## 5. Listado de facturas

- Tabla: proveedor, nº factura, fecha, vencimiento, base, IVA, total, estado, fecha de pago.
- **Filtros:** estado, proveedor, forma de pago, rango de fechas (factura o vencimiento), texto libre.
- Orden por cualquier columna. Por defecto: fecha de factura descendente.
- **Resaltado** de facturas vencidas y no pagadas.
- Pie con **nº de facturas y suma de totales** del filtro.
- Doble clic → ficha de la factura (datos, PDF, historial de estados).
- Botón "Abrir PDF" (visor del sistema).

## 6. Estados

```
Recibida ──► Validada ──► Pagada
   │            │
   └────────────┴──► Rechazada/Anulada
```

- **Validada:** revisada y conforme para pago.
- **Pagada:** pide **fecha de pago** (por defecto hoy).
- **Rechazada/Anulada:** pide **motivo**.
- Se permite volver atrás un estado (p. ej. Pagada → Validada por error), dejando rastro.
- Cambio de estado individual o **masivo** (selección múltiple en el listado).
- Cada cambio queda en `FacturaEstadoHistorico`.
- Editar datos de la factura solo en estado Recibida o Validada.

## 7. Gestión de proveedores

- Listado con búsqueda (razón social, CIF).
- Alta, edición y **baja lógica** (no se borran si tienen facturas).
- Ficha del proveedor con sus facturas.
- Los cambios de IBAN quedan registrados (histórico).

## 8. Modelo de datos (BD `FACTURASCEFER`)

### `Proveedor`
| Columna | Tipo | Notas |
|---|---|---|
| Id | int IDENTITY PK | |
| RazonSocial | nvarchar(200) | obligatorio |
| CIF | varchar(20) | único, obligatorio |
| Direccion | nvarchar(250) | |
| CP | varchar(10) | |
| Poblacion | nvarchar(100) | |
| Provincia | nvarchar(100) | |
| Pais | nvarchar(60) | por defecto "España" |
| IBAN | varchar(34) | |
| FormaPago | tinyint | 1 Transferencia, 2 Domiciliación, 3 Tarjeta |
| Tarjeta | nvarchar(60) NULL | tarjeta habitual (marca + últimos 4) |
| Email | nvarchar(150) | |
| Telefono | varchar(30) | |
| Observaciones | nvarchar(max) | |
| Baja | bit | default 0 |
| FechaAlta | datetime2 | |
| IdUsuarioAlta | int | → DMSTRA.dbo.Usuarios |

### `FacturaProveedores`
| Columna | Tipo | Notas |
|---|---|---|
| Id | int IDENTITY PK | |
| IdProveedor | int FK → Proveedor | |
| NumeroFactura | nvarchar(50) | único con IdProveedor |
| Concepto | nvarchar(500) NULL | extraído por la IA |
| FechaFactura | date | |
| FechaVencimiento | date NULL | |
| BaseImponible | decimal(12,2) | |
| PorcIVA | decimal(5,2) | |
| CuotaIVA | decimal(12,2) | |
| PorcIRPF | decimal(5,2) NULL | |
| CuotaIRPF | decimal(12,2) NULL | |
| Total | decimal(12,2) | |
| IBAN | varchar(34) | el de la factura (transferencia: del proveedor; domiciliación: cuenta de cargo de CEFER) |
| FormaPago | tinyint | 1 Transferencia, 2 Domiciliación, 3 Tarjeta |
| Tarjeta | nvarchar(60) NULL | tarjeta con la que se pagó (marca + últimos 4) |
| Estado | tinyint | 1 Recibida, 2 Validada, 3 Pagada, 4 Rechazada/Anulada |
| FechaPago | date NULL | |
| MotivoRechazo | nvarchar(500) NULL | |
| RutaPdf | nvarchar(400) | ruta UNC del PDF de esta factura |
| RutaPdfOriginal | nvarchar(400) | ruta UNC del PDF subido (compartida si traía varias) |
| PaginaInicio / PaginaFin | smallint | páginas dentro del original |
| NombreOriginal | nvarchar(255) | nombre del fichero subido |
| JsonExtraccionIA | nvarchar(max) | respuesta bruta de Claude |
| Observaciones | nvarchar(max) | |
| FechaRegistro | datetime2 | |
| IdUsuarioRegistro | int | |

### `FacturaEstadoHistorico`
| Columna | Tipo |
|---|---|
| Id | int IDENTITY PK |
| IdFactura | int FK |
| EstadoAnterior | tinyint NULL |
| EstadoNuevo | tinyint |
| Fecha | datetime2 |
| IdUsuario | int |
| Comentario | nvarchar(500) |

### `ProveedorIbanHistorico`
Id, IdProveedor, IbanAnterior, IbanNuevo, Fecha, IdUsuario, IdFacturaOrigen (NULL).

Script de creación en `sql/2026-09-25-crear-bd-facturascefer.sql`. El login `programacion` necesita mapeo en la BD nueva (db_datareader/db_datawriter) y lectura en `DMSTRA`.

## 9. Configuración (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Facturas": "Server=192.168.0.15;Database=FACTURASCEFER;User Id=programacion;Password=***;Encrypt=false",
    "Dmstra":   "Server=192.168.0.15;Database=DMSTRA;User Id=programacion;Password=***;Encrypt=false"
  },
  "RutaFacturas": "\\\\192.168.0.10\\Cefer\\FacturasProveedores",
  "CifPropio": "B00000000",
  "Claude": { "ApiKey": "sk-ant-***", "Model": "claude-opus-5" }
}
```

## 10. Privacidad y seguridad

- ⚠️ **Las facturas pueden contener datos de salud**: nombre de pacientes y servicio prestado (p. ej. facturas de clínicas colaboradoras). Enviarlas a la API de Claude es un tratamiento de datos por un encargado: **validar con el DPO** y valorar solicitar a Anthropic retención cero (ZDR).
- Carpeta `FacturasProveedores` con permisos restringidos a administración.
- API key y contraseñas solo en `appsettings.json` (fuera del repo).
- La alerta de cambio de IBAN es la principal medida antifraude.

## 11. Despliegue y requisitos del PC

- Un único `FacturasCefer.exe` + `appsettings.json` (no requiere instalar .NET).
- Red a SQL-01 y escritura en la ruta de facturas.
- Salida a internet hacia `api.anthropic.com`.
- WebView2 Runtime (viene de serie en Windows 10/11 actualizados).

## 12. Fases

| Fase | Contenido |
|---|---|
| F1 | BD + login + gestión de proveedores |
| F2 | Caja de subida + extracción IA (varias facturas por PDF) + revisión + guardado |
| F3 | Listado, filtros, estados e historial |
| F4 (opcional) | Exportar a Excel, remesa SEPA |

## 13. Decisiones y pendientes

**Resuelto:**
- Una sola tabla de proveedores (`Proveedor`); cada proveedor se identifica por su CIF.
- Un PDF puede traer varias facturas → se separan en PDFs individuales.
- Sin roles.
- Carpeta: `\\192.168.0.10\Cefer\FacturasProveedores`.

**Pendiente:**
- Facturas rectificativas: se tratarán cuando aparezca el caso (de momento se registran como una factura normal con importes negativos).
- Permisos de escritura en la carpeta de red para los usuarios de la app.
