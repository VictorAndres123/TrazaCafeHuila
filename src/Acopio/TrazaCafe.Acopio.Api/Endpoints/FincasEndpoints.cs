using Microsoft.AspNetCore.Http.HttpResults;
using TrazaCafe.Acopio.Application.Consultas;

namespace TrazaCafe.Acopio.Api.Endpoints;

public static class FincasEndpoints
{
    public static IEndpointRouteBuilder MapFincasEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fincas", async Task<Ok<IReadOnlyList<FincaResumenDto>>> (IGenealogiaReadModel lectura, CancellationToken ct) =>
                TypedResults.Ok(await lectura.ListarFincasAsync(ct)))
            .WithTags("Fincas")
            .WithName("ListarFincas")
            .WithSummary("Fincas registradas (datos sintéticos de prueba)");
        return app;
    }
}
