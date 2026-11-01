# ADR-005 · Integridad de escritura: idempotencia por ID de cliente + hash y concurrencia optimista

- **Estado:** Aceptado
- **Fecha:** 2026-10-31

## Contexto
Dos amenazas a QA1: reenvíos tras cortes de conexión, y dos analistas asignando la misma entrega a lotes distintos al mismo tiempo.

## Decisión
- `entregaId` es un UUID generado por el cliente; se guarda el SHA-256 de su representación canónica (`id|finca|PUNTO|kilos con 3 decimales|fecha`).
- Mismo ID y mismo hash → se omite (200, `duplicadas`). Mismo ID y hash distinto → **409** y la solicitud completa es atómica (no se persiste nada).
- `PRIMARY KEY(id)` es la última defensa ante dos reenvíos simultáneos; la API reintenta una vez y el segundo intento ve las entregas como duplicadas.
- La asignación de entregas a un lote usa la columna de sistema `xmin` de PostgreSQL como token de concurrencia en EF Core (`UPDATE … WHERE xmin = @original`). `DbUpdateConcurrencyException` se traduce en 409. `UNIQUE(entrega_id)` en `lote_entregas` es una segunda defensa del invariante I2.

## Alternativas
- Cabecera `Idempotency-Key` por petición: no cubre reenvíos parciales que mezclan entregas nuevas y viejas.
- Bloqueo pesimista (`SELECT … FOR UPDATE`): más contención y bloqueos largos.

## Consecuencias
El cliente debe generar UUID estables. Los campos del hash quedan documentados; cambiarlos rompe la idempotencia. El 409 obliga a reintentar con datos frescos.
