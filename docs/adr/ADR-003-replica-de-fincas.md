# ADR-003 · Réplica local de geometrías de fincas en Cumplimiento

- **Estado:** Aceptado
- **Fecha:** 2026-10-31

## Contexto
Cumplimiento necesita los polígonos de las fincas para verificar, pero esos datos son propiedad del contexto de Acopio.

## Decisión
El evento `FincaRegistrada` (v1) lleva el identificador y la geometría GeoJSON. Cumplimiento mantiene `fincas_replica` **sin datos personales del caficultor** (minimización). El *seed* de fincas en Acopio inserta esos eventos en el outbox, de modo que la réplica se forma por el mismo camino que en producción. Cada servicio tiene su base de datos lógica (`acopio_db`, `cumplimiento_db`) sobre una sola instancia de PostgreSQL: simplificación consciente del MVP.

## Alternativas
- Consulta HTTP síncrona a Acopio: crea acoplamiento temporal y viola QA2.
- Incluir las geometrías en `LoteConformado`: viable y más simple, pero produce mensajes pesados y duplica datos en cada lote.

## Consecuencias
- (+) Autonomía de Cumplimiento.
- (−) Datos duplicados y eventualmente consistentes. Si `LoteConformado` llega antes que la finca, el consumidor lanza `FincaNoReplicadaException` y el mensaje se reintenta.
- Actualizar un polígono exigiría un evento `FincaActualizada`, fuera del MVP.
