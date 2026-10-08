/* ============================================================
   FACTURASCEFER — importación automática de facturas (versión 1.6.0)

   1. FacturaProveedores.Origen     1 = Manual (app), 2 = Correo (importador automático)
      FacturaProveedores.Revisar    1 = requiere revisión humana (alta automática, IBAN distinto,
                                        no cuadra, datos dudosos…) · MotivoRevision: por qué
   2. Proveedor.PendienteRevision   1 = dado de alta automáticamente, falta revisar su ficha
   3. FacturaImportacion            registro de cada PDF procesado por el importador
                                    (evita procesarlo dos veces y deja trazabilidad)

   Solo añade columnas y una tabla. Ejecutar en SQL-01. Idempotente.
   ============================================================ */

USE FACTURASCEFER;
GO

IF COL_LENGTH(N'dbo.FacturaProveedores', N'Origen') IS NULL
    ALTER TABLE dbo.FacturaProveedores ADD Origen tinyint NOT NULL
        CONSTRAINT DF_Factura_Origen DEFAULT (1)
        CONSTRAINT CK_Factura_Origen CHECK (Origen IN (1, 2));
GO
IF COL_LENGTH(N'dbo.FacturaProveedores', N'Revisar') IS NULL
    ALTER TABLE dbo.FacturaProveedores ADD Revisar bit NOT NULL CONSTRAINT DF_Factura_Revisar DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.FacturaProveedores', N'MotivoRevision') IS NULL
    ALTER TABLE dbo.FacturaProveedores ADD MotivoRevision nvarchar(500) NULL;
GO

IF COL_LENGTH(N'dbo.Proveedor', N'PendienteRevision') IS NULL
    ALTER TABLE dbo.Proveedor ADD PendienteRevision bit NOT NULL CONSTRAINT DF_Proveedor_PendienteRevision DEFAULT (0);
GO

IF OBJECT_ID(N'dbo.FacturaImportacion', N'U') IS NULL
CREATE TABLE dbo.FacturaImportacion (
    Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaImportacion PRIMARY KEY,
    Fuente           varchar(20)       NOT NULL,              -- 'drive'
    IdExterno        varchar(200)      NOT NULL,              -- id del fichero en Google Drive
    NombreFichero    nvarchar(400)     NOT NULL,
    CorreoRemitente  nvarchar(200)     NULL,
    CorreoAsunto     nvarchar(500)     NULL,
    Fecha            datetime2(0)      NOT NULL CONSTRAINT DF_FacturaImportacion_Fecha DEFAULT (SYSDATETIME()),
    Resultado        varchar(20)       NOT NULL,              -- procesada | duplicada | sin_factura | error
    Mensaje          nvarchar(max)     NULL,
    IdsFacturas      varchar(400)      NULL,                  -- ids de FacturaProveedores creados (separados por coma)
    CONSTRAINT UQ_FacturaImportacion_Externo UNIQUE (Fuente, IdExterno)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_Revisar')
    CREATE INDEX IX_Factura_Revisar ON dbo.FacturaProveedores (Revisar) WHERE Revisar = 1;
GO
