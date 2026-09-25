/* ============================================================
   FACTURASCEFER — deshacer la tabla Empresa
   (Empresa y Proveedor son lo mismo: solo existe Proveedor.)
   Ejecutar en SQL-01 con un usuario administrador. Idempotente.
   ============================================================ */

USE FACTURASCEFER;
GO

-- 1. Índice que usa IdEmpresa
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_Empresa_Estado')
    DROP INDEX IX_Factura_Empresa_Estado ON dbo.FacturaProveedores;
GO

-- 2. FK y columna IdEmpresa en FacturaProveedores
IF OBJECT_ID(N'dbo.FK_Factura_Empresa', N'F') IS NOT NULL
    ALTER TABLE dbo.FacturaProveedores DROP CONSTRAINT FK_Factura_Empresa;
GO
IF COL_LENGTH(N'dbo.FacturaProveedores', N'IdEmpresa') IS NOT NULL
    ALTER TABLE dbo.FacturaProveedores DROP COLUMN IdEmpresa;
GO

-- 3. Tabla Empresa
IF OBJECT_ID(N'dbo.Empresa', N'U') IS NOT NULL
    DROP TABLE dbo.Empresa;
GO

-- 4. Índice de listado sin empresa
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_Estado')
    CREATE INDEX IX_Factura_Estado ON dbo.FacturaProveedores (Estado, FechaFactura);
GO
