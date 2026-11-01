-- TrazaCafé Huila · 02 · Datos de prueba SINTÉTICOS (fincas ficticias; no corresponden a predios ni personas reales)
\connect acopio_db

INSERT INTO fincas (id, codigo, nombre, municipio, vereda, geometria, consentimiento_datos, registrada_en) VALUES
('f1000000-0000-4000-8000-000000000001', 'F-001', 'La Esperanza', 'Pitalito', 'Bruselas',
 '{"type":"Polygon","coordinates":[[[-76.060,1.840],[-76.050,1.840],[-76.050,1.850],[-76.060,1.850],[-76.060,1.840]]]}', true, now()),
('f1000000-0000-4000-8000-000000000002', 'F-002', 'El Mirador', 'Acevedo', 'San Adolfo',
 '{"type":"Polygon","coordinates":[[[-75.900,1.800],[-75.890,1.800],[-75.890,1.810],[-75.900,1.810],[-75.900,1.800]]]}', true, now()),
('f1000000-0000-4000-8000-000000000003', 'F-003', 'Buenavista', 'La Plata', 'Belén',
 '{"type":"Polygon","coordinates":[[[-75.900,2.380],[-75.890,2.380],[-75.890,2.390],[-75.900,2.390],[-75.900,2.380]]]}', true, now()),
('f1000000-0000-4000-8000-000000000004', 'F-004', 'Los Naranjos', 'Garzón', 'Zuluaga',
 '{"type":"Polygon","coordinates":[[[-75.640,2.190],[-75.630,2.190],[-75.630,2.200],[-75.640,2.200],[-75.640,2.190]]]}', true, now()),
('f1000000-0000-4000-8000-000000000005', 'F-005', 'San Isidro', 'Timaná', 'Cosanza',
 '{"type":"Polygon","coordinates":[[[-75.940,1.960],[-75.930,1.960],[-75.930,1.970],[-75.940,1.970],[-75.940,1.960]]]}', true, now()),
('f1000000-0000-4000-8000-000000000006', 'F-006', 'El Recuerdo', 'Suaza', 'Guayabal',
 '{"type":"Polygon","coordinates":[[[-75.800,1.970],[-75.790,1.970],[-75.790,1.980],[-75.800,1.980],[-75.800,1.970]]]}', false, now());

-- Cada finca registrada genera su evento FincaRegistrada por el MISMO camino que en producción (outbox → RabbitMQ),
-- de modo que Cumplimiento construye su réplica de geometrías (ADR-003).
INSERT INTO outbox_messages (id, tipo, payload, ocurrido_en)
SELECT gen_random_uuid(),
       'acopio.finca-registrada.v1',
       jsonb_build_object(
           'fincaId', f.id,
           'codigo', f.codigo,
           'geometriaGeoJson', f.geometria::text,
           'registradaEn', f.registrada_en),
       f.registrada_en
FROM fincas f
ORDER BY f.codigo;
