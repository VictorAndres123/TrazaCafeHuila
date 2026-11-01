using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TrazaCafe.Acopio.Application.Comandos;
using TrazaCafe.Acopio.Application.Errores;

namespace TrazaCafe.Acopio.Api.Endpoints;

public sealed record SincronizarEntregasRequest(string? PuntoAcopio, IReadOnlyList<EntregaRequest>? Entregas);

/// <param name="EntregaId">UUID generado por el cliente al registrar la entrega (aun sin conexión).</param>
public sealed record EntregaRequest(Guid EntregaId, Guid FincaId, decimal Kilos, DateOnly FechaEntrega);

public sealed record SincronizacionResponse(int Creadas, int Duplicadas, IReadOnlyList<Guid> IdsCreados, IReadOnlyList<Guid> IdsDuplicados);

public static class EntregasEndpoints
{
    public static IEndpointRouteBuilder MapEntregasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/entregas").WithTags("Entregas (RF1)");

        grupo.MapPost("/sync", SincronizarAsync)
            .WithName("SincronizarEntregas")
            .WithSummary("Sincroniza entregas registradas sin conexión (idempotente)")
            .WithDescription("Reenviar el mismo lote responde 200 sin duplicar kilos. Mismo entregaId con contenido distinto responde 409 y no persiste nada.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<Results<Ok<SincronizacionResponse>, Conflict<ProblemDetails>>> SincronizarAsync(
        SincronizarEntregasRequest request, SincronizarEntregasHandler casoDeUso, CancellationToken ct)
    {
        var comando = new SincronizarEntregasCommand(
            request.PuntoAcopio ?? "",
            (request.Entregas ?? []).Select(e => new EntregaPorSincronizar(e.EntregaId, e.FincaId, e.Kilos, e.FechaEntrega)).ToList());

        ResultadoSincronizacion resultado;
        try
        {
            resultado = await casoDeUso.EjecutarAsync(comando, ct);
        }
        catch (ConflictoConcurrenciaException)
        {
            // Dos reenvíos simultáneos del mismo lote: la restricción única decidió el ganador.
            // Se reintenta una vez; ahora las entregas aparecen como duplicadas (200).
            resultado = await casoDeUso.EjecutarAsync(comando, ct);
        }

        return resultado switch
        {
            ResultadoSincronizacion.Aceptada a => TypedResults.Ok(
                new SincronizacionResponse(a.Creadas.Count, a.Duplicadas.Count, a.Creadas, a.Duplicadas)),

            ResultadoSincronizacion.Conflicto c => TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Entregas en conflicto",
                Detail = "Existen entregas con el mismo identificador y contenido distinto. No se persistió ninguna entrega de esta solicitud.",
                Extensions = { ["entregasEnConflicto"] = c.EnConflicto }
            }),

            _ => throw new InvalidOperationException("Resultado de sincronización no soportado.")
        };
    }
}
