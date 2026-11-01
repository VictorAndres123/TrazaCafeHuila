# ADR-004 · Consistencia eventual explícita en el ciclo de vida del lote

- **Estado:** Aceptado
- **Fecha:** 2026-10-31

## Contexto
La verificación es asíncrona y el usuario no debe creer que un lote sin verificar ya es declarable.

## Decisión
Máquina de estados `PendienteVerificacion → Declarable | Bloqueado`. Solo `Lote.AplicarVerificacion` cambia el estado; basta una finca no conforme para que el lote quede `Bloqueado`, y los estados finales son inmutables. `POST /api/lotes` responde **202 Accepted** con `Location` a la genealogía, que expone `estado`, `conformadoEn`, `verificadoEn` y `segundosHastaVerificacion`. Una restricción `CHECK` en la tabla `lotes` impide estados finales sin fecha de verificación.

## Alternativas
- Verificar de forma síncrona dentro del POST: acopla la API a una dependencia lenta y viola QA2.
- Ocultar el estado intermedio: deja al usuario sin información.

## Consecuencias
El cliente consulta el estado (polling). La ventana de inconsistencia es visible y medible: es la métrica de QA2.
