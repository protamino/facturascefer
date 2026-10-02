/* ============================================================
   FACTURASCEFER — carga de cuentas desde el plan contable de a3ASESOR
   (Listado del Plan Contable, empresa 00007, 02/10/2026)
   1. Cuenta de proveedor (400/410) de 24 proveedores, emparejados por CIF
      (solo se rellena si está vacía).
   2. 266 cuentas del grupo 6 (gastos/compras) en el catálogo CuentaContable
      (solo las que no existan). No borra ni modifica nada más.
   Idempotente.
   ============================================================ */
USE FACTURASCEFER;
SET XACT_ABORT ON;
BEGIN TRAN;

/* 1. Cuenta de proveedor */
UPDATE dbo.Proveedor SET CuentaProveedor = '41000240' WHERE Id = 30 AND CuentaProveedor IS NULL;  -- Ajuntament de Barcelona - Institut Municipal d'Hisenda (P0801900B)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000367' WHERE Id = 28 AND CuentaProveedor IS NULL;  -- AUUPA CONSULTORÍA Y SERVICIOS S.L. (B65969891)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000006' WHERE Id = 19 AND CuentaProveedor IS NULL;  -- BIOMEDICAL SUPPLY, S.L. (B97887319)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000491' WHERE Id = 31 AND CuentaProveedor IS NULL;  -- CITIOR, S.L (NACEX) (B59886192)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000300' WHERE Id = 24 AND CuentaProveedor IS NULL;  -- Clàudia Pérez Pay (47420046A)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000324' WHERE Id = 1 AND CuentaProveedor IS NULL;  -- Clínica GMA Barcelona S.L. (B16780942)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000336' WHERE Id = 26 AND CuentaProveedor IS NULL;  -- COLL GARCES LABORATORI S.A. (A58459223)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000095' WHERE Id = 14 AND CuentaProveedor IS NULL;  -- CONCILE DIAGNOSTICS S.L. (B66099029)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000066' WHERE Id = 22 AND CuentaProveedor IS NULL;  -- DURVIZ, S.L. (B46072807)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000013' WHERE Id = 3 AND CuentaProveedor IS NULL;  -- Equipos Medico-Biológicos BCN SL (B65387722)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000321' WHERE Id = 29 AND CuentaProveedor IS NULL;  -- Ferrero Servicios Medicos (B65690760)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000538' WHERE Id = 5 AND CuentaProveedor IS NULL;  -- FLY VET EUROPA SRL (B90349473)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000527' WHERE Id = 6 AND CuentaProveedor IS NULL;  -- GENERAL LIM, S.L. (B60678851)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000221' WHERE Id = 32 AND CuentaProveedor IS NULL;  -- Grupo de Servicios Montemar, S.L. (TOT TERRENY) (B58417171)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000222' WHERE Id = 18 AND CuentaProveedor IS NULL;  -- Laundry online, SL (B65866022)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000002' WHERE Id = 27 AND CuentaProveedor IS NULL;  -- Linde Gas España, S.A.U. (A08007262)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000017' WHERE Id = 21 AND CuentaProveedor IS NULL;  -- NIRCO S.L. (B58786096)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000233' WHERE Id = 8 AND CuentaProveedor IS NULL;  -- NOMINALIA INTERNET, S.L. (B61553327)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000004' WHERE Id = 2 AND CuentaProveedor IS NULL;  -- SUMINISTROS HOSPITALARIOS, S.A. (A08876310)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000025' WHERE Id = 13 AND CuentaProveedor IS NULL;  -- TELEFONICA DE ESPAÑA, S.A.U. (A82018474)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000373' WHERE Id = 20 AND CuentaProveedor IS NULL;  -- Validated ID, S.L.U. (B65750721)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000334' WHERE Id = 23 AND CuentaProveedor IS NULL;  -- Vitrolife Medical Devices Spain, S.L (B56554835)
UPDATE dbo.Proveedor SET CuentaProveedor = '41000505' WHERE Id = 15 AND CuentaProveedor IS NULL;  -- VVO HEALTHCARE SLP (B44740637)
UPDATE dbo.Proveedor SET CuentaProveedor = '40000141' WHERE Id = 7 AND CuentaProveedor IS NULL;  -- DINASCIENCE, S.A. (A63260764)

/* 2. Catálogo: grupo 6 */
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60000000', N'COMPRAS DE MERCADERÍAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60100000', N'COMPRAS DE MATERIAS PRIMAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60200000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60200000', N'COMPRAS DE OTROS APROVISIONAMI');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60200001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60200001', N'DONANTES DE SEMEN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60200002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60200002', N'DONANTES DE OVULOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60600000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60600000', N'DTOS. S/COMPRAS P.P. MERCADER.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60610000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60610000', N'DTOS. S/COMPRAS P.P. M. PRIMAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60620000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60620000', N'DTOS. S/COMPRAS P.P. OTR.APRO.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700000', N'TRABAJOS REALIZADOS POR OTRAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700001', N'ANESTESIAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700002', N'ANALISIS Y PRUEBAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700003', N'PORTA DE LA RIVA, NURIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700004') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700004', N'GAUTHIER CASAUX, GUIL');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700005') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700005', N'SANCHEZ BALLESTER, FRANCISCO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700006') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700006', N'LOPEZ BAEZA, FERNANDO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700007') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700007', N'MOLFINO, MARIA FLORENCIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700008') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700008', N'QUALIMEDIC');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700009') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700009', N'REPROGENETICS SPAIN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700010') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700010', N'GARCIA RODRIGUEZ, XAVIER');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700011') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700011', N'SANZ MARTIN, PABLO JOSE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700012') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700012', N'MARINA AVENDAÑO, SIMON');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700013') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700013', N'GARCIA ABREU, TANIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700015') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700015', N'PASCUAL, XAVIER');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700016') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700016', N'JOVE ALEGRE, INMACULADA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700018') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700018', N'VALLS I RICART, GEMMA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700019') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700019', N'ASESORIA DE FERTILIDAD');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700020') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700020', N'HERRERO VICENTE, GERMAN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700021') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700021', N'MARTINEZ GONZALEZ, ESTEFANIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700022') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700022', N'MOLINS ESPINOSA, JORGE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700023') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700023', N'PUCHOL CASTILLO, JORGE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700024') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700024', N'CESPA GR, SA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700025') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700025', N'MEDINAMIC 2010, SL');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700026') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700026', N'VOLPE, LAURA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700027') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700027', N'COLABORADORES PROFESIONALES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700028') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700028', N'AJUSTES CONT');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700029') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700029', N'PRADA QUEIPO, ELENA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700030') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700030', N'CRISTINA CABERO RIERA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700031') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700031', N'JANISSE FERRERI DOS ANSOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60700032') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60700032', N'DINASCIENCE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60800000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60800000', N'DEVOLUCIONES DE COMPRAS DE MER');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60810000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60810000', N'DEVOL. COMPRAS MATERIAS PRIMAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60820000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60820000', N'DEVOL. COMPRAS OTROS APROVISI.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60900000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60900000', N'“RAPPELS” POR COMPRAS DE MERCA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60910000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60910000', N'“RAPPELS” POR COMPRAS DE MATER');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '60920000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('60920000', N'RAPPELS COMPRAS OTROS APROVIO.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '61000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('61000000', N'VARIACIÓN DE EXISTENCIAS DE ME');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '61000001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('61000001', N'VAR. EXIST. FUNGIBLES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '61100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('61100000', N'VARIACIÓN EXISTENCIAS M PRIMAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '61100001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('61100001', N'VARIACION EXIST.BANCO DE SEMEN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '61100002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('61100002', N'VAR. EXIST OVOCITOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '61100003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('61100003', N'VAR. EXIST. EMBRIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '61200000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('61200000', N'VARIACIÓN EXISTE. OTROS APROV.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62000000', N'GASTOS I+D EJERCICIO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62100000', N'ARRENDAMIENTOS Y CÁNONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62100011') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62100011', N'GASTOS ESCALERA PORTERO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62110000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62110000', N'CAIXARENTING');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62120000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62120000', N'ARRENDAMIENTO DE LOCALES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62130000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62130000', N'ALQUILER SIMON MARINA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62140000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62140000', N'ALQUILER GARAJE C/ GRAN VIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62200000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62200000', N'REPARACIONES Y CONSERVACIÓN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62200001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62200001', N'REPARACIONES INMUEBLES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62200002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62200002', N'REPARACIONES VEHICULOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62200003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62200003', N'MANTENIMIENTO INFORMATICO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62200004') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62200004', N'PEQUEÑAS REPARACIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62200005') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62200005', N'MANT. MAQ(ASSI,CLIMATIZACION..');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62200006') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62200006', N'MANTENIMIENTO CLIMATIZACION');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62200007') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62200007', N'MANTENIMIENTO PUERTAS AUT.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300000', N'SERVICIOS PROFESIONALES INDEP.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300001', N'GESTORIAS Y ASESORIAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300002', N'NOTARIOS Y ABOGADOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300003', N'OLIVER BOSOM, SYLVIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300006') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300006', N'CERTIFICACIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300007') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300007', N'TRADUCCIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300009') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300009', N'SOPORTES Y REDES, SL');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300010') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300010', N'ANCORA DUAL');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300011') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300011', N'BABSOFTWARE APPLICATION');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300012') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300012', N'REGISTRO MERCANTIL');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62300013') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62300013', N'NOMINALIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62310000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62310000', N'SERVICIOS COLABORADORES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62400000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62400000', N'TRANSPORTES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62400001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62400001', N'PORTES COMPRAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62500000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62500000', N'PRIMAS DE SEGUROS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62600000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62600000', N'SERVICIOS BANCARIOS Y SIMILARE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62600001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62600001', N'BANSABADELL FINCOM EFC,');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62600002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62600002', N'GASTOS CUENTA CREDITO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62700000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62700000', N'PUBLICID., PROPAGANDA Y RR.PP.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62700001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62700001', N'PUBLICIDAD EN INTERNET');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62700002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62700002', N'PUBLICIDAD EN MEDIOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62700004') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62700004', N'RRPP(RESTAURANTES)');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62700005') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62700005', N'PUBLICIDAD VALLA LLEIDA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62700006') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62700006', N'PUBLICIDAD MUPIS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62700007') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62700007', N'PUBLICIDAD VALENCIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62800000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62800000', N'SUMINISTROS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62800001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62800001', N'ELECTRICIDAD');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62800002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62800002', N'AGUA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62800003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62800003', N'GAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62800004') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62800004', N'TELEFONO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900000', N'OTROS SERVICIOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900001', N'PEAJES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900002', N'MATERIAL DE OFICINA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900003', N'GASTOS DE LIMPIEZA Y TINTORERI');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900004') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900004', N'GASTOS ALIMENTACION');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900005') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900005', N'GASOLINA Y PARKINGS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900006') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900006', N'TAXIS DESPLAZAMIENTOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900007') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900007', N'VESTUARIO Y CALZADO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900008') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900008', N'SUSCRIPCIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900009') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900009', N'VIA T');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900010') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900010', N'ASOCIACIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900011') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900011', N'SELLOS Y CORREOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900012') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900012', N'DESPLAZAMIENTOS(RENFE..)');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900013') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900013', N'PLEYADE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900014') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900014', N'FUENTES DE AGUA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900015') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900015', N'GASTOS COMUNITARIOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900016') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900016', N'CERT. DIGITAL FNMT');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900017') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900017', N'PREVENCION DE INCENDIOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900018') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900018', N'FAX');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900019') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900019', N'FORMACIÓN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900020') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900020', N'SEGURIDAD');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62900021') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62900021', N'GASTOS INTERNET,AI');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62920000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62920000', N'OTROS GASTOS DEDUCIBLES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62920016') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62920016', N'CERTIFICADO FNMT');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '62930001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('62930001', N'GASTOS CONGRESOS_SIMP_FORMACIO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63000000', N'IMPUESTO CORRIENTE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63010000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63010000', N'IMPUESTO DIFERIDO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63100000', N'OTROS TRIBUTOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63100001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63100001', N'RECARGOS Y SANCIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63100002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63100002', N'TASAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63100003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63100003', N'IBI');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63100004') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63100004', N'IVTM');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63300000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63300000', N'AJUST. NEGAT. EN IMPOSIC. S/B');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63410000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63410000', N'AJUSTES NEGAT. IVA ACTIVO CTE.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63420000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63420000', N'AJUSTES NEGATIVOS EN IVA DE IN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63600000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63600000', N'DEVOLUCIÓN DE IMPUESTOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63800000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63800000', N'AJUST. POSIT. EN IMPOSIC. S/B');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63910000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63910000', N'AJUST. POSIT. IVA ACTIVO CTE.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '63920000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('63920000', N'AJUSTES POSITIVOS EN IVA DE IN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64000000', N'SUELDOS Y SALARIOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64000001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64000001', N'NOMINAS BCN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64000002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64000002', N'NOMINAS VALENCIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64100000', N'INDEMNIZACIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64200000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64200000', N'SEGURIDAD SOCIAL A CARGO DE LA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64200001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64200001', N'SEG.SOCIAL BCN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64200002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64200002', N'SEG. SOCIAL LLEIDA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64200003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64200003', N'SEG. SOCIAL VALENCIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64300000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64300000', N'RETRIB. LP MEDI.SIST.APOR.DEF.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64400000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64400000', N'CONTRIBUCIONES ANUALES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64420000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64420000', N'OTROS COSTES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64500000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64500000', N'RETR.AL PER. LIQ.C/INSTR.PATR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64570000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64570000', N'RETR.AL PER.LIQ.EFE.B/INST.PAT');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64900000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64900000', N'OTROS GASTOS SOCIALES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '64900001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('64900001', N'OTROS GASTOS SOCIALES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '65000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('65000000', N'PERD. CRÉD. COMER. INCOBRABLES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '65100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('65100000', N'BENEFICIO TRANSFERIDO (GESTOR)');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '65110000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('65110000', N'PÉRD.SOPORT.(PARTÍ. NO GESTOR)');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '65900000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('65900000', N'OTRAS PÉRDIDAS EN GESTIÓN CORR');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66000000', N'GASTOS FNOS. ACTUALI. PROVISI.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66000001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66000001', N'INTERESES DE DEMORA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66100000', N'INTE.OBLI. Y BONOS LP,EMP. GR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66110000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66110000', N'INTE.OBLI. Y BONOS LP,EMP. AS.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66120000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66120000', N'INTE.OBLI. Y BONOS LP,OTR.VIN.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66130000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66130000', N'INTE.OBLI. Y BONOS LP,OTR. EMP');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66150000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66150000', N'INTE.OBLI. Y BONOS CP,EMP. GR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66160000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66160000', N'INTE.OBLI. Y BONOS CP,EMP. AS.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66170000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66170000', N'INTE.OBLI. Y BONOS CP,OTR.VIN.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66180000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66180000', N'INTE.OBLI. Y BONOS CP,OTR.EMP.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66200000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66200000', N'INTERESES DE DEUDAS, EMPRESAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66210000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66210000', N'INTERESES DE DEUDAS, EMPRESAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66220000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66220000', N'INTE. DEUDAS, OTR. PART. VINC.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66220001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66220001', N'MARINA AVENDAÑO, FERNANDO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66220002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66220002', N'MARINA RONCERO, DAVID');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230000', N'INTE. DEUDAS CON ENT. CRÉDITO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230001', N'ICO LA CAIXA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230002', N'BBVA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230003') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230003', N'LEASING HISTEROSCOPIO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230004') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230004', N'LEASING CENTRIFUGA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230005') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230005', N'LEASING AUTOCLAVE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230006') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230006', N'INT DEUDA POL.CREDITO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230007') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230007', N'LEASING EMBRYO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230008') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230008', N'LEASING MONITORES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230009') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230009', N'LEASING IMSI- ICSI');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230010') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230010', N'INTERESES PRESTAMO UNICO LA CA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230011') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230011', N'INTERESES COCHE IBIZA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230012') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230012', N'INT. PTMO B. SANTANDER');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66230013') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66230013', N'SIMON MARINA RONCERO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66240000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66240000', N'INTERESES DE DEUDAS, OTRAS EMP');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66300000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66300000', N'PÉRDIDAS DE CARTERA DE NEGOCIA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66310000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66310000', N'PÉRDIDAS DE DESIGNADOS POR LA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66320000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66320000', N'PÉRDIDAS DE DISPONIBLES PARA L');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66330000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66330000', N'PÉRDIDAS DE INSTRUMENTOS DE CO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66400000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66400000', N'DIVIDENDOS PASIVOS, EMP. GRUPO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66410000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66410000', N'DIVIDENDOS PASIVOS, EMP. AS.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66420000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66420000', N'DIVID. PAS., OTRAS PARTES VIN.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66430000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66430000', N'DIVIDENDOS DE PASIVOS, OTRAS E');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66500000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66500000', N'INT. DTO EFE. ENT. CRÉDITO GR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66510000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66510000', N'INTE. DTO EFE. ENT. CRÉDI. AS.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66520000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66520000', N'INTE.DTO EFE.OTR.ENT.CRÉD.VIN.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66530000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66530000', N'INTE. DTO EFE. OTRAS ENT.CRÉD.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66540000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66540000', N'INTE.OP.FACTORING ENT.CRÉD.GR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66550000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66550000', N'INT.OP.FACTORING ENT.CRÉD. A.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66560000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66560000', N'INT.OP.FACTORING OTR.ENT.CRÉD.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66570000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66570000', N'INT.OP.FACTORING OTR.ENT.CRÉD.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66600000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66600000', N'PERD.VAL.REPRE.DEUDA LP,EMP GR');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66610000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66610000', N'PERD.VAL.REPRE.DEUDA LP, EMP.A');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66620000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66620000', N'PERD.VAL.REPR.DEUDA LP,OTR.VIN');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66630000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66630000', N'PER.PARTI.REPRE.DDA LP,OTR.EMP');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66650000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66650000', N'PERD.PARTI.REPRE.DDA CP,EMP.GR');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66660000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66660000', N'PERD.PARTI.REPRE.DDA CP,EMP.AS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66670000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66670000', N'PERD.VAL.REPRE.DDA CP,OTR.VIN.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66680000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66680000', N'PERD.VAL.REPRE.DDA CP,OTR.EMP.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66700000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66700000', N'PÉRD. CRÉDITOS LP, EMP. GRUPO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66710000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66710000', N'PÉRD. CRÉDITOS LP, EMP. ASOCI.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66720000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66720000', N'PÉRD. CRÉDITOS LP, OTRAS VINC.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66730000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66730000', N'PÉRD. CRÉDITOS LP, OTRAS EMPR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66750000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66750000', N'PERD. CRÉDITOS CP,EMPRESAS GR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66760000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66760000', N'PERD. CRÉDITOS CP, EMP. ASOCI.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66770000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66770000', N'PERD. CRÉDITOS CP, OTRAS VINC.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66780000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66780000', N'PERD. CRÉDITOS CP, OTRAS EMPR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66800000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66800000', N'DIFERENCIAS NEGATIVAS DE CAMBI');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '66900000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('66900000', N'OTROS GASTOS FINANCIEROS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67000000', N'PÉRD. PROCED. INMO. INTANGIBLE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67100000', N'PÉRD. PROCED. INMOV. MATERIAL');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67200000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67200000', N'PERD. PROCED. INVER. INMOBILI.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67330000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67330000', N'PERD. PROC. PARTI. LP, EMP.GR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67340000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67340000', N'PERD. PROC. PARTI. LP,EMP. AS.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67350000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67350000', N'PERD.PROC.PARTI. LP, OTR.VINC.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67500000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67500000', N'PERD. OPERAC. C/OBLIG. PROPIAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67800000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67800000', N'GASTOS EXCEPCIONALES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67800001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67800001', N'DONATIVOS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '67800002') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('67800002', N'INTERESES DEMORA HACIENDA');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '68000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('68000000', N'AMORTIZACIÓN DEL INMOVILIZADO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '68100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('68100000', N'AMORTIZACIÓN DEL INMOVILIZADO');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '68140001') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('68140001', N'AMORTIZACION RESPIRADOR PULMON');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '68140005') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('68140005', N'DOTACIONES PARA AMORTIZACIONES');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '68200000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('68200000', N'AMORTIZ. DE INV. INMOBILIARIAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69000000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69000000', N'PÉRD. DETER. INMOV. INTANGIBLE');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69100000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69100000', N'PÉRD. DETER. INMOVIL. MATERIAL');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69200000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69200000', N'PERD. DETER. INVER. INMOBILIA.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69300000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69300000', N'PER.DETE.PROD.TERM. Y CUR.FABR');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69310000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69310000', N'PÉRDIDAS POR DETERIORO DE MERC');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69320000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69320000', N'PÉRD. DETER. MATERIAS PRIMAS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69330000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69330000', N'PERD. DETER. OTROS APROVISION.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69400000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69400000', N'PERD. DETER. CRED. OP. COMERC.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69540000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69540000', N'DOTA. A LA PROVI.CONTRAT.ONER.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69590000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69590000', N'DOTA. A LA PROVI. OTR.OP.COMER');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69600000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69600000', N'PER.DET.PARTI. INST.PN LP,EM.G');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69610000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69610000', N'PER.DET.PARTI. INSTR.PN LP,E.A');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69620000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69620000', N'PE.DET.PARTI. INST.PN LP,OTR.V');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69630000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69630000', N'PE.DET.PARTI. INST.PN LP,OTR.E');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69650000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69650000', N'PER.DET.VAL.REPR.DDA LP,EMP.GR');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69660000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69660000', N'PER.DET.VAL.REPR.DDA LP,EMP.AS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69670000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69670000', N'PER.DET.VAL.REPR.DDA LP,OTR.VI');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69680000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69680000', N'PER.DET.VAL.REPR.DDA LP,OTR.EM');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69700000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69700000', N'PERD. DETER. CRED. LP,EMP. GR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69710000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69710000', N'PERD. DETER. CRED. LP,EMP. AS.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69720000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69720000', N'PERD. DETER. CRED. LP,OTR. VI.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69730000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69730000', N'PERD. DETER. CRED. LP,OTR. EM.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69800000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69800000', N'PER.DET.PARTI.INSTR.PN CP,E.GR');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69810000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69810000', N'PER.DET.PARTI. INSTR.PN CP,E.A');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69850000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69850000', N'PER.DET.VAL.REPR.DDA CP,EMP.GR');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69860000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69860000', N'PER.DET.VAL.REPR.DDA CP,EMP.AS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69870000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69870000', N'PER.DET.VAL.REPR.DDA CP,OTR.VI');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69880000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69880000', N'PER.DET.VAL.REPR.DDA CP,OTR.EM');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69900000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69900000', N'PERD. DETER. CRED. CP,EMP. GR.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69910000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69910000', N'PERD. DETER. CRED. CP,EMP. AS');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69920000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69920000', N'PERD. DETER. CRED. CP,OTR.VIN.');
IF NOT EXISTS (SELECT 1 FROM dbo.CuentaContable WHERE Codigo = '69930000') INSERT INTO dbo.CuentaContable (Codigo, Descripcion) VALUES ('69930000', N'PERD. DETER. CRED. CP,OTR. EMP');

COMMIT;

SELECT (SELECT COUNT(*) FROM dbo.Proveedor WHERE CuentaProveedor IS NOT NULL) AS ProveedoresConCuenta,
       (SELECT COUNT(*) FROM dbo.CuentaContable WHERE Codigo LIKE '6%') AS CuentasGrupo6;
