/* ============================================================
   FACTURASCEFER — desglose de impuestos + exportación a a3ASESOR | con
   (versión de la app 1.5.0)

   1. FacturaImpuesto: líneas de impuesto de cada factura (una por tipo de IVA /
      recargo / cuenta de gasto). La cabecera de FacturaProveedores mantiene los
      totales (suma de las líneas).
   2. Migración NO destructiva: crea una línea por cada factura existente copiando
      la base / IVA de la cabecera. No modifica ni borra nada.
   3. Proveedor.CuentaProveedor: cuenta contable del proveedor (400xxxxx).
   4. FacturaExportacion: historial de exportaciones (no bloquea re-exportar).

   Ejecutar en SQL-01. Idempotente.
   ============================================================ */

USE FACTURASCEFER;
GO

/* ---------- 1. Líneas de impuesto ---------- */
IF OBJECT_ID(N'dbo.FacturaImpuesto', N'U') IS NULL
CREATE TABLE dbo.FacturaImpuesto (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaImpuesto PRIMARY KEY,
    IdFactura       int               NOT NULL CONSTRAINT FK_FacturaImpuesto_Factura
                                      REFERENCES dbo.FacturaProveedores(Id) ON DELETE CASCADE,
    Orden           smallint          NOT NULL,
    BaseImponible   decimal(12,2)     NOT NULL,
    PorcIVA         decimal(5,2)      NOT NULL,
    CuotaIVA        decimal(12,2)     NOT NULL,
    PorcRE          decimal(5,2)      NULL,   -- recargo de equivalencia (solo si la factura lo indica)
    CuotaRE         decimal(12,2)     NULL,
    CuentaContable  char(8)           NULL    -- cuenta de gasto de la línea; NULL = la de la factura
                    CONSTRAINT FK_FacturaImpuesto_Cuenta REFERENCES dbo.CuentaContable(Codigo)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FacturaImpuesto_Factura')
    CREATE INDEX IX_FacturaImpuesto_Factura ON dbo.FacturaImpuesto (IdFactura, Orden);
GO

/* ---------- 2. Migración: una línea por factura que aún no tenga ninguna ---------- */
INSERT INTO dbo.FacturaImpuesto (IdFactura, Orden, BaseImponible, PorcIVA, CuotaIVA)
SELECT f.Id, 1, ISNULL(f.BaseImponible, 0), ISNULL(f.PorcIVA, 0), ISNULL(f.CuotaIVA, 0)
FROM dbo.FacturaProveedores f
WHERE NOT EXISTS (SELECT 1 FROM dbo.FacturaImpuesto i WHERE i.IdFactura = f.Id);
GO

/* ---------- 3. Cuenta del proveedor (400xxxxx) ---------- */
IF COL_LENGTH(N'dbo.Proveedor', N'CuentaProveedor') IS NULL
    ALTER TABLE dbo.Proveedor ADD CuentaProveedor char(8) NULL;
GO

/* ---------- 4. Historial de exportaciones ---------- */
IF OBJECT_ID(N'dbo.FacturaExportacion', N'U') IS NULL
CREATE TABLE dbo.FacturaExportacion (
    Id          int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FacturaExportacion PRIMARY KEY,
    IdFactura   int               NOT NULL CONSTRAINT FK_FacturaExportacion_Factura REFERENCES dbo.FacturaProveedores(Id),
    Destino     varchar(20)       NOT NULL,          -- 'A3'
    Fecha       datetime2(0)      NOT NULL CONSTRAINT DF_FacturaExportacion_Fecha DEFAULT (SYSDATETIME()),
    IdUsuario   int               NOT NULL,
    Archivo     nvarchar(400)     NOT NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FacturaExportacion_Factura')
    CREATE INDEX IX_FacturaExportacion_Factura ON dbo.FacturaExportacion (IdFactura, Destino, Fecha);
GO

/* Comprobación */
SELECT (SELECT COUNT(*) FROM dbo.FacturaProveedores) AS Facturas,
       (SELECT COUNT(DISTINCT IdFactura) FROM dbo.FacturaImpuesto) AS FacturasConLineas;
GO
