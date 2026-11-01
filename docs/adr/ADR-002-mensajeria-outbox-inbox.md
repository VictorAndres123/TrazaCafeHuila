# ADR-002 · Mensajería confiable: RabbitMQ + Transactional Outbox + Inbox

- **Estado:** Aceptado
- **Fecha:** 2026-10-31

## Contexto
Escribir en la base de datos y publicar en el broker son dos operaciones; si el proceso cae entre ambas se pierden eventos (*dual write*). La auditabilidad no tolera eventos perdidos.

## Decisión
- El evento se guarda en `outbox_messages` **en la misma transacción** que el cambio de negocio (un único `SaveChanges`).
- `OutboxRelay<TDbContext>` (BackgroundService) lo publica después: sondeo cada 1 s, `FOR UPDATE SKIP LOCKED`, *publisher confirms* y mensajes persistentes.
- El consumidor registra el `messageId` en `inbox_messages` en la misma transacción que su efecto.
- Reintentos: cola `<cola>.retry` con TTL de 5 s que devuelve el mensaje a la cola principal por *dead-lettering*; tras 5 intentos, `<cola>.dlq`. Los cuerpos inválidos van directo a la DLQ.
- Se usa `RabbitMQ.Client` directamente, sin framework de mensajería, para que el flujo sea explícito y defendible.

## Alternativas
- Kafka: el *replay* y el alto throughput no son drivers; es más pesado de operar.
- Publicar después del commit: pierde eventos si el proceso cae entre ambos pasos.
- CDC con Debezium: más infraestructura.

## Consecuencias
- La entrega es **al menos una vez**; los efectos son **exactamente una vez** solo gracias al inbox.
- Hasta ~1 s de latencia añadida por el sondeo.
- La topología del broker (exchange, colas, bindings, `.retry`, `.dlq`) está definida como código en `TopologiaTrazaCafe` y la declara cualquier servicio al arrancar (idempotente). Así ningún evento se publica antes de que exista la cola suscriptora; en producción esto pasaría a IaC del broker.
- El outbox no se purga en el MVP y funciona como diario de auditoría; en producción se archivaría.
