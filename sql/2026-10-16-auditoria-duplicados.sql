/*
    FACTURASCEFER 1.9.0 — Auditoría de cambios y detección de duplicados.
    Ejecutar en SQL-01, base de datos FACTURASCEFER, ANTES de instalar la app 1.9.0 y el importador 1.9.0.
    Se puede ejecutar más de una vez.

    1. FacturaCambio: cada cambio de datos de una factura (campo, valor anterior → nuevo, usuario y fecha).
       Los cambios de estado siguen en FacturaEstadoHistorico.
    2. FacturaProveedores.HashPdf: huella SHA-256 del PDF subido, para detectar el mismo PDF subido dos veces.
*/
USE FACTURASCEFER;
GO

IF OBJECT_ID(N'dbo.FacturaCambio', N'U') IS NULL
CREATE TABLE dbo.FacturaCambio (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaCambio PRIMARY KEY,
    IdFactura       int               NOT NULL CONSTRAINT FK_FacturaCambio_Factura REFERENCES dbo.FacturaProveedores(Id),
    Fecha           datetime2(0)      NOT NULL CONSTRAINT DF_FacturaCambio_Fecha DEFAULT (SYSDATETIME()),
    IdUsuario       int               NOT NULL,              -- DMSTRA.dbo.Usuarios
    Campo           nvarchar(60)      NOT NULL,
    ValorAnterior   nvarchar(400)     NULL,
    ValorNuevo      nvarchar(400)     NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FacturaCambio_Factura')
    CREATE INDEX IX_FacturaCambio_Factura ON dbo.FacturaCambio (IdFactura, Fecha DESC);
GO

IF COL_LENGTH(N'dbo.FacturaProveedores', N'HashPdf') IS NULL
    ALTER TABLE dbo.FacturaProveedores ADD HashPdf char(64) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_HashPdf')
    CREATE INDEX IX_Factura_HashPdf ON dbo.FacturaProveedores (HashPdf) WHERE HashPdf IS NOT NULL;
GO

-- Comprobación: 1, 1, 2
SELECT
    (SELECT COUNT(*) FROM sys.tables  WHERE name = N'FacturaCambio')                                              AS FacturaCambio,
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.FacturaProveedores') AND name = N'HashPdf') AS HashPdf,
    (SELECT COUNT(*) FROM sys.indexes WHERE name IN (N'IX_FacturaCambio_Factura', N'IX_Factura_HashPdf'))          AS Indices;
