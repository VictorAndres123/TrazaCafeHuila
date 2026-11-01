using Microsoft.AspNetCore.Http.HttpResults;
using TrazaCafe.Acopio.Application.Comandos;
using TrazaCafe.Acopio.Application.Consultas;

namespace TrazaCafe.Acopio.Api.Endpoints;

public sealed record ConformarLoteRequest(IReadOnlyList<Guid>? EntregaIds);

public static class LotesEndpoints
{
    public static IEndpointRouteBuilder MapLotesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/lotes").WithTags("Lotes (RF2–RF4)");

        // ---- Comando ----
        grupo.MapPost("/", ConformarAsync)
            .WithName("ConformarLote")
            .WithSummary("Conforma un lote de exportación (verificación asíncrona)")
            .WithDescription("Responde 202: el lote queda en PendienteVerificacion hasta que Cumplimiento publique LoteVerificado.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // ---- Consultas ----
        grupo.MapGet("/", ListarAsync)
            .WithName("ListarLotes")
            .WithSummary("Lista los lotes con su estado");

        grupo.MapGet("/{loteId:guid}/genealogia", GenealogiaAsync)
            .WithName("GenealogiaLote")
            .WithSummary("Genealogía del lote: lote → fincas → entregas → verificación")
            .WithDescription("Insumo para la declaración de diligencia debida que presenta el operador en la UE.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Accepted<LoteConformadoResultado>> ConformarAsync(
        ConformarLoteRequest request, ConformarLoteHandler casoDeUso, CancellationToken ct)
    {
        var resultado = await casoDeUso.EjecutarAsync(new ConformarLoteCommand(request.EntregaIds ?? []), ct);
        return TypedResults.Accepted($"/api/lotes/{resultado.LoteId}/genealogia", resultado);
    }

    private static async Task<Ok<IReadOnlyList<LoteResumenDto>>> ListarAsync(IGenealogiaReadModel lectura, CancellationToken ct) =>
        TypedResults.Ok(await lectura.ListarLotesAsync(ct));

    private static async Task<Ok<GenealogiaLoteDto>> GenealogiaAsync(
        Guid loteId, ConsultarGenealogiaHandler consulta, CancellationToken ct) =>
        TypedResults.Ok(await consulta.EjecutarAsync(loteId, ct));
}
