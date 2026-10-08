/* ============================================================
   FACTURASCEFER — creación de BD y tablas (Fase 1)
   Ejecutar en SQL-01 (192.168.0.15) con un usuario administrador.
   Idempotente: se puede relanzar sin error.
   ============================================================ */

IF DB_ID(N'FACTURASCEFER') IS NULL
    CREATE DATABASE FACTURASCEFER;
GO

USE FACTURASCEFER;
GO

/* ---------- CuentaContable (catálogo, código de 8 dígitos) ---------- */
IF OBJECT_ID(N'dbo.CuentaContable', N'U') IS NULL
CREATE TABLE dbo.CuentaContable (
    Codigo         char(8)        NOT NULL CONSTRAINT PK_CuentaContable PRIMARY KEY
                   CONSTRAINT CK_CuentaContable_Codigo CHECK (Codigo LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
    Descripcion    nvarchar(150)  NOT NULL,
    Baja           bit            NOT NULL CONSTRAINT DF_CuentaContable_Baja DEFAULT (0),
    FechaAlta      datetime2(0)   NOT NULL CONSTRAINT DF_CuentaContable_FechaAlta DEFAULT (SYSDATETIME()),
    IdUsuarioAlta  int            NULL
);
GO

/* ---------- Proveedor ---------- */
IF OBJECT_ID(N'dbo.Proveedor', N'U') IS NULL
CREATE TABLE dbo.Proveedor (
    Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Proveedor PRIMARY KEY,
    RazonSocial    nvarchar(200)     NOT NULL,
    CIF            varchar(20)       NOT NULL,
    Direccion      nvarchar(250)     NULL,
    CP             varchar(10)       NULL,
    Poblacion      nvarchar(100)     NULL,
    Provincia      nvarchar(100)     NULL,
    Pais           nvarchar(60)      NOT NULL CONSTRAINT DF_Proveedor_Pais DEFAULT (N'España'),
    IBAN           varchar(34)       NULL,
    FormaPago      tinyint           NOT NULL CONSTRAINT DF_Proveedor_FormaPago DEFAULT (1)
                   CONSTRAINT CK_Proveedor_FormaPago CHECK (FormaPago IN (1, 2, 3)), -- 1 Transferencia, 2 Domiciliación, 3 Tarjeta
    Tarjeta        nvarchar(60)      NULL,  -- tarjeta habitual: marca + últimos 4 dígitos
    CuentaContable char(8)           NULL CONSTRAINT FK_Proveedor_CuentaContable REFERENCES dbo.CuentaContable(Codigo), -- por defecto
    CuentaProveedor char(8)          NULL,  -- cuenta contable del proveedor (400xxxxx) para la exportación a A3
    PendienteRevision bit            NOT NULL CONSTRAINT DF_Proveedor_PendienteRevision DEFAULT (0), -- alta automática sin revisar
    Email          nvarchar(150)     NULL,
    Telefono       varchar(30)       NULL,
    Observaciones  nvarchar(max)     NULL,
    Baja           bit               NOT NULL CONSTRAINT DF_Proveedor_Baja DEFAULT (0),
    FechaAlta      datetime2(0)      NOT NULL CONSTRAINT DF_Proveedor_FechaAlta DEFAULT (SYSDATETIME()),
    IdUsuarioAlta  int               NULL,
    CONSTRAINT UQ_Proveedor_CIF UNIQUE (CIF)
);
GO

/* ---------- FacturaProveedores ---------- */
IF OBJECT_ID(N'dbo.FacturaProveedores', N'U') IS NULL
CREATE TABLE dbo.FacturaProveedores (
    Id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaProveedores PRIMARY KEY,
    IdProveedor        int               NOT NULL CONSTRAINT FK_Factura_Proveedor REFERENCES dbo.Proveedor(Id),
    NumeroFactura      nvarchar(50)      NOT NULL,
    Concepto           nvarchar(500)     NULL,
    FechaFactura       date              NOT NULL,
    FechaVencimiento   date              NULL,
    BaseImponible      decimal(12,2)     NULL,
    PorcIVA            decimal(5,2)      NULL,
    CuotaIVA           decimal(12,2)     NULL,
    PorcIRPF           decimal(5,2)      NULL,
    CuotaIRPF          decimal(12,2)     NULL,
    Total              decimal(12,2)     NOT NULL,
    IBAN               varchar(34)       NULL,  -- transferencia: cuenta del proveedor; domiciliación: cuenta de cargo de CEFER
    FormaPago          tinyint           NOT NULL CONSTRAINT DF_Factura_FormaPago DEFAULT (1)
                       CONSTRAINT CK_Factura_FormaPago CHECK (FormaPago IN (1, 2, 3)),
    Tarjeta            nvarchar(60)      NULL,  -- pagada con tarjeta: marca + últimos 4 dígitos
    CuentaContable     char(8)           NULL CONSTRAINT FK_Factura_CuentaContable REFERENCES dbo.CuentaContable(Codigo),
    Estado             tinyint           NOT NULL CONSTRAINT DF_Factura_Estado DEFAULT (1)
                       CONSTRAINT CK_Factura_Estado CHECK (Estado IN (1,2,3,4)), -- 1 Recibida, 2 Validada, 3 Pagada, 4 Rechazada/Anulada
    FechaPago          date              NULL,
    MotivoRechazo      nvarchar(500)     NULL,
    RutaPdf            nvarchar(400)     NOT NULL,
    RutaPdfOriginal    nvarchar(400)     NULL,
    PaginaInicio       smallint          NULL,
    PaginaFin          smallint          NULL,
    NombreOriginal     nvarchar(255)     NULL,
    JsonExtraccionIA   nvarchar(max)     NULL,
    Observaciones      nvarchar(max)     NULL,
    Origen             tinyint           NOT NULL CONSTRAINT DF_Factura_Origen DEFAULT (1)
                       CONSTRAINT CK_Factura_Origen CHECK (Origen IN (1, 2)),   -- 1 Manual, 2 Correo (importador)
    Revisar            bit               NOT NULL CONSTRAINT DF_Factura_Revisar DEFAULT (0),
    MotivoRevision     nvarchar(500)     NULL,
    FechaRegistro      datetime2(0)      NOT NULL CONSTRAINT DF_Factura_FechaRegistro DEFAULT (SYSDATETIME()),
    IdUsuarioRegistro  int               NOT NULL,
    CONSTRAINT UQ_Factura_Proveedor_Numero UNIQUE (IdProveedor, NumeroFactura)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_Estado')
    CREATE INDEX IX_Factura_Estado ON dbo.FacturaProveedores (Estado, FechaFactura);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_CuentaContable')
    CREATE INDEX IX_Factura_CuentaContable ON dbo.FacturaProveedores (CuentaContable);
GO

/* ---------- FacturaImpuesto (desglose: una línea por tipo de IVA / recargo / cuenta) ---------- */
IF OBJECT_ID(N'dbo.FacturaImpuesto', N'U') IS NULL
CREATE TABLE dbo.FacturaImpuesto (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaImpuesto PRIMARY KEY,
    IdFactura       int               NOT NULL CONSTRAINT FK_FacturaImpuesto_Factura
                                      REFERENCES dbo.FacturaProveedores(Id) ON DELETE CASCADE,
    Orden           smallint          NOT NULL,
    BaseImponible   decimal(12,2)     NOT NULL,
    PorcIVA         decimal(5,2)      NOT NULL,
    CuotaIVA        decimal(12,2)     NOT NULL,
    PorcRE          decimal(5,2)      NULL,
    CuotaRE         decimal(12,2)     NULL,
    CuentaContable  char(8)           NULL CONSTRAINT FK_FacturaImpuesto_Cuenta REFERENCES dbo.CuentaContable(Codigo)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FacturaImpuesto_Factura')
    CREATE INDEX IX_FacturaImpuesto_Factura ON dbo.FacturaImpuesto (IdFactura, Orden);
GO

/* ---------- FacturaExportacion (historial de exportaciones a contabilidad) ---------- */
IF OBJECT_ID(N'dbo.FacturaExportacion', N'U') IS NULL
CREATE TABLE dbo.FacturaExportacion (
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaExportacion PRIMARY KEY,
    IdFactura   int               NOT NULL CONSTRAINT FK_FacturaExportacion_Factura REFERENCES dbo.FacturaProveedores(Id),
    Destino     varchar(20)       NOT NULL,
    Fecha       datetime2(0)      NOT NULL CONSTRAINT DF_FacturaExportacion_Fecha DEFAULT (SYSDATETIME()),
    IdUsuario   int               NOT NULL,
    Archivo     nvarchar(400)     NOT NULL
);
GO

/* ---------- FacturaImportacion (registro del importador automático) ---------- */
IF OBJECT_ID(N'dbo.FacturaImportacion', N'U') IS NULL
CREATE TABLE dbo.FacturaImportacion (
    Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaImportacion PRIMARY KEY,
    Fuente           varchar(20)       NOT NULL,
    IdExterno        varchar(200)      NOT NULL,
    NombreFichero    nvarchar(400)     NOT NULL,
    CorreoRemitente  nvarchar(200)     NULL,
    CorreoAsunto     nvarchar(500)     NULL,
    Fecha            datetime2(0)      NOT NULL CONSTRAINT DF_FacturaImportacion_Fecha DEFAULT (SYSDATETIME()),
    Resultado        varchar(20)       NOT NULL,
    Mensaje          nvarchar(max)     NULL,
    IdsFacturas      varchar(400)      NULL,
    CONSTRAINT UQ_FacturaImportacion_Externo UNIQUE (Fuente, IdExterno)
);
GO

/* ---------- FacturaEstadoHistorico ---------- */
IF OBJECT_ID(N'dbo.FacturaEstadoHistorico', N'U') IS NULL
CREATE TABLE dbo.FacturaEstadoHistorico (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaEstadoHistorico PRIMARY KEY,
    IdFactura       int               NOT NULL CONSTRAINT FK_Historico_Factura REFERENCES dbo.FacturaProveedores(Id),
    EstadoAnterior  tinyint           NULL,
    EstadoNuevo     tinyint           NOT NULL,
    Fecha           datetime2(0)      NOT NULL CONSTRAINT DF_Historico_Fecha DEFAULT (SYSDATETIME()),
    IdUsuario       int               NOT NULL,
    Comentario      nvarchar(500)     NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Historico_Factura')
    CREATE INDEX IX_Historico_Factura ON dbo.FacturaEstadoHistorico (IdFactura);
GO

/* ---------- ProveedorIbanHistorico ---------- */
IF OBJECT_ID(N'dbo.ProveedorIbanHistorico', N'U') IS NULL
CREATE TABLE dbo.ProveedorIbanHistorico (
    Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProveedorIbanHistorico PRIMARY KEY,
    IdProveedor      int               NOT NULL CONSTRAINT FK_IbanHist_Proveedor REFERENCES dbo.Proveedor(Id),
    IbanAnterior     varchar(34)       NULL,
    IbanNuevo        varchar(34)       NULL,
    Fecha            datetime2(0)      NOT NULL CONSTRAINT DF_IbanHist_Fecha DEFAULT (SYSDATETIME()),
    IdUsuario        int               NOT NULL,
    IdFacturaOrigen  int               NULL CONSTRAINT FK_IbanHist_Factura REFERENCES dbo.FacturaProveedores(Id)
);
GO

/* ---------- Permisos para el login de la app ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'programacion')
    CREATE USER programacion FOR LOGIN programacion;
GO
ALTER ROLE db_datareader ADD MEMBER programacion;
ALTER ROLE db_datawriter ADD MEMBER programacion;
GO
