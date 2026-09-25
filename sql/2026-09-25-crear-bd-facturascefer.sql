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
    FechaRegistro      datetime2(0)      NOT NULL CONSTRAINT DF_Factura_FechaRegistro DEFAULT (SYSDATETIME()),
    IdUsuarioRegistro  int               NOT NULL,
    CONSTRAINT UQ_Factura_Proveedor_Numero UNIQUE (IdProveedor, NumeroFactura)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_Estado')
    CREATE INDEX IX_Factura_Estado ON dbo.FacturaProveedores (Estado, FechaFactura);
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
