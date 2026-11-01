# ADR-006 · Verificación geoespacial con NetTopologySuite en memoria detrás de un puerto; PostGIS como evolución

- **Estado:** Aceptado
- **Fecha:** 2026-10-31

## Contexto
Plazo de 24 horas y QA4: cambiar de proveedor geoespacial sin tocar el dominio. PostGIS es una pieza más de infraestructura que depurar.

## Decisión
- Puerto `IVerificadorDeforestacion.BuscarPerdidasAsync(geometríaGeoJson)` en la capa de aplicación de Cumplimiento. **El adaptador solo informa las pérdidas de bosque que intersectan la finca (id del área y fecha de pérdida).**
- **La regla regulatoria vive en el dominio:** `ReglaEudr` (fecha de corte 31-12-2020) y `ResultadoFinca.Evaluar` deciden si la finca es conforme. Así, cambiar de proveedor no puede alterar la regla.
- Adaptador `NtsVerificadorDeforestacion`: carga al iniciar un GeoJSON sintético de áreas con fecha de pérdida, las indexa en un `STRtree` (filtro por envolvente) y evalúa `Intersects` (filtro exacto).

## Alternativas
- PostGIS con `ST_Intersects` e índices GiST: escala mejor; queda como evolución.
- API externa tipo Global Forest Watch: no viable en el plazo (credenciales, límites de uso).

## Consecuencias
- Solo sirve para capas que caben en memoria.
- Pasar a PostGIS será la demostración de QA4: un adaptador nuevo y una línea de registro en `DependencyInjection`, con dominio y aplicación intactos.
- Los resultados con datos sintéticos no tienen valor probatorio y así se declara como limitación.
