/* ============================================================
   FACTURASCEFER — campo Concepto en FacturaProveedores (Fase 2)
   Descripción breve del servicio, extraída por la IA.
   Ejecutar en SQL-01. Idempotente.
   ============================================================ */

USE FACTURASCEFER;
GO

IF COL_LENGTH(N'dbo.FacturaProveedores', N'Concepto') IS NULL
    ALTER TABLE dbo.FacturaProveedores ADD Concepto nvarchar(500) NULL;
GO
