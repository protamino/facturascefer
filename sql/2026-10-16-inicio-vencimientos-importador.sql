/*
    FACTURASCEFER 1.8.0 — Inicio, vencimientos y monitor del importador.
    Ejecutar en SQL-01, base de datos FACTURASCEFER, ANTES de instalar la app 1.8.0 y el importador 1.8.0.
    Se puede ejecutar más de una vez.

    1. Proveedor.DiasPago: días desde la fecha de factura para calcular el vencimiento cuando la factura no lo trae.
    2. ImportadorEstado: una sola fila con la última vez que el servicio importador revisó Drive (latido).
    3. Índices para el panel de inicio y el listado de importaciones.
*/
USE FACTURASCEFER;
GO

IF COL_LENGTH(N'dbo.Proveedor', N'DiasPago') IS NULL
    ALTER TABLE dbo.Proveedor ADD DiasPago smallint NULL
        CONSTRAINT CK_Proveedor_DiasPago CHECK (DiasPago BETWEEN 0 AND 365);
GO

IF OBJECT_ID(N'dbo.ImportadorEstado', N'U') IS NULL
CREATE TABLE dbo.ImportadorEstado (
    Id               tinyint        NOT NULL CONSTRAINT PK_ImportadorEstado PRIMARY KEY
                                             CONSTRAINT CK_ImportadorEstado_Id CHECK (Id = 1),
    UltimaPasada     datetime2(0)   NOT NULL,
    Correcta         bit            NOT NULL,              -- 0 si la última pasada terminó con error
    Mensaje          nvarchar(500)  NULL,                  -- resumen de la última pasada o del error
    Equipo           nvarchar(100)  NULL,
    Version          varchar(20)    NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_Vencimiento')
    CREATE INDEX IX_Factura_Vencimiento ON dbo.FacturaProveedores (FechaVencimiento)
        INCLUDE (Estado, Total) WHERE Estado IN (1, 2);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FacturaImportacion_Fecha')
    CREATE INDEX IX_FacturaImportacion_Fecha ON dbo.FacturaImportacion (Fecha DESC) INCLUDE (Resultado);
GO

-- Comprobación
SELECT
    (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Proveedor') AND name = N'DiasPago') AS DiasPago,
    (SELECT COUNT(*) FROM sys.tables  WHERE name = N'ImportadorEstado')                                   AS ImportadorEstado,
    (SELECT COUNT(*) FROM sys.indexes WHERE name IN (N'IX_Factura_Vencimiento', N'IX_FacturaImportacion_Fecha')) AS Indices;
