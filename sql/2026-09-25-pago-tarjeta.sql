/* ============================================================
   FACTURASCEFER — forma de pago 3 = Tarjeta
   Las facturas pagadas con tarjeta se registran directamente como Pagadas.
   Tarjeta: solo marca y últimos 4 dígitos (p. ej. "VISA ···· 1234"), NUNCA el número completo.
   Ejecutar en SQL-01. Idempotente.
   ============================================================ */

USE FACTURASCEFER;
GO

-- FormaPago admite 3 (Tarjeta)
IF OBJECT_ID(N'dbo.CK_Proveedor_FormaPago', N'C') IS NOT NULL
    ALTER TABLE dbo.Proveedor DROP CONSTRAINT CK_Proveedor_FormaPago;
GO
ALTER TABLE dbo.Proveedor ADD CONSTRAINT CK_Proveedor_FormaPago CHECK (FormaPago IN (1, 2, 3));
GO

IF OBJECT_ID(N'dbo.CK_Factura_FormaPago', N'C') IS NOT NULL
    ALTER TABLE dbo.FacturaProveedores DROP CONSTRAINT CK_Factura_FormaPago;
GO
ALTER TABLE dbo.FacturaProveedores ADD CONSTRAINT CK_Factura_FormaPago CHECK (FormaPago IN (1, 2, 3));
GO

-- Tarjeta habitual del proveedor y tarjeta con la que se pagó cada factura
IF COL_LENGTH(N'dbo.Proveedor', N'Tarjeta') IS NULL
    ALTER TABLE dbo.Proveedor ADD Tarjeta nvarchar(60) NULL;
GO
IF COL_LENGTH(N'dbo.FacturaProveedores', N'Tarjeta') IS NULL
    ALTER TABLE dbo.FacturaProveedores ADD Tarjeta nvarchar(60) NULL;
GO
