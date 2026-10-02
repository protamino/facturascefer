/* ============================================================
   FACTURASCEFER — cuentas contables
   Catálogo CuentaContable (código de 8 dígitos) + cuenta por defecto del proveedor
   + cuenta de cada factura (se hereda del proveedor y se puede cambiar).
   Ejecutar en SQL-01. Idempotente.
   ============================================================ */

USE FACTURASCEFER;
GO

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

IF COL_LENGTH(N'dbo.Proveedor', N'CuentaContable') IS NULL
    ALTER TABLE dbo.Proveedor ADD CuentaContable char(8) NULL
        CONSTRAINT FK_Proveedor_CuentaContable REFERENCES dbo.CuentaContable(Codigo);
GO

IF COL_LENGTH(N'dbo.FacturaProveedores', N'CuentaContable') IS NULL
    ALTER TABLE dbo.FacturaProveedores ADD CuentaContable char(8) NULL
        CONSTRAINT FK_Factura_CuentaContable REFERENCES dbo.CuentaContable(Codigo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Factura_CuentaContable')
    CREATE INDEX IX_Factura_CuentaContable ON dbo.FacturaProveedores (CuentaContable);
GO
