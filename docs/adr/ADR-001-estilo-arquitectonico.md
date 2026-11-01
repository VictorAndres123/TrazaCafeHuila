# ADR-001 · Estilo: dos servicios hexagonales orientados a eventos, con CQRS ligero

- **Estado:** Aceptado
- **Fecha:** 2026-10-31

## Contexto
Los drivers QA1 (integridad), QA2 (disponibilidad / aislamiento de fallas), QA3 (auditabilidad) y QA4 (modificabilidad) deben atenderse con un equipo de una persona y un plazo de 24 horas. La verificación geoespacial es lenta, depende de un tercero cambiante y escala con el volumen de fincas, no con el de entregas.

## Decisión
Dos servicios desplegables, **Acopio y Lotes** y **Cumplimiento**, cada uno con capas hexagonales (`Domain`, `Application`, `Infrastructure`, `Api`/`Worker`) separadas en proyectos para que el compilador haga cumplir la regla de dependencias. En Acopio, los comandos pasan por agregados con EF Core y las consultas leen una vista SQL con Npgsql sin tocar el dominio (CQRS ligero, mismo almacén).

**Plan B:** si el flujo de punta a punta no corre a la hora de corte, se pasa a un monolito modular con el mismo dominio y la misma aplicación; el bus pasa a ser en proceso y se mantiene el outbox. El cambio es barato porque el broker está detrás de un puerto.

## Alternativas
- Tres servicios (Consulta aparte): descartado; no aporta frente a su costo.
- Monolito modular desde el inicio: válido; se compara en la evaluación.
- Event Sourcing: descartado por complejidad para el plazo.

## Consecuencias
- (+) Aislamiento de fallas y escalado independiente de la verificación.
- (−) Consistencia eventual, más infraestructura y contratos de eventos que versionar.
