/* ============================================================
   FACTURASCEFER — forma de pago (transferencia / domiciliación)
   1 = Transferencia (CEFER paga al IBAN del proveedor)
   2 = Domiciliación (el proveedor gira un recibo a la cuenta de CEFER;
       el IBAN de la factura es entonces la cuenta de cargo de CEFER)
   Ejecutar en SQL-01. Idempotente.
   ============================================================ */

USE FACTURASCEFER;
GO

IF COL_LENGTH(N'dbo.Proveedor', N'FormaPago') IS NULL
    ALTER TABLE dbo.Proveedor ADD FormaPago tinyint NOT NULL
        CONSTRAINT DF_Proveedor_FormaPago DEFAULT (1)
        CONSTRAINT CK_Proveedor_FormaPago CHECK (FormaPago IN (1, 2));
GO

IF COL_LENGTH(N'dbo.FacturaProveedores', N'FormaPago') IS NULL
    ALTER TABLE dbo.FacturaProveedores ADD FormaPago tinyint NOT NULL
        CONSTRAINT DF_Factura_FormaPago DEFAULT (1)
        CONSTRAINT CK_Factura_FormaPago CHECK (FormaPago IN (1, 2));
GO
