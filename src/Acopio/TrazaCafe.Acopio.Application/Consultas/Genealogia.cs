using System.Text.Json;
using TrazaCafe.Acopio.Application.Errores;

namespace TrazaCafe.Acopio.Application.Consultas;

// ---- Lado de lectura (CQRS): no pasa por el dominio ni por EF Core ----

public sealed record GenealogiaLoteDto(
    Guid LoteId,
    string Codigo,
    string Estado,
    decimal KilosTotales,
    DateTimeOffset ConformadoEn,
    DateTimeOffset? VerificadoEn,
    double? SegundosHastaVerificacion,
    IReadOnlyList<FincaEnLoteDto> Fincas);

public sealed record FincaEnLoteDto(
    Guid FincaId,
    string Codigo,
    string Nombre,
    string Municipio,
    string Vereda,
    JsonElement Geometria,
    decimal Kilos,
    bool? Conforme,
    string? DetalleVerificacion,
    IReadOnlyList<EntregaEnLoteDto> Entregas);

public sealed record EntregaEnLoteDto(Guid EntregaId, DateOnly FechaEntrega, string PuntoAcopio, decimal Kilos);

public sealed record LoteResumenDto(
    Guid LoteId, string Codigo, string Estado, decimal KilosTotales,
    int Entregas, int Fincas, DateTimeOffset ConformadoEn, DateTimeOffset? VerificadoEn);

public sealed record FincaResumenDto(
    Guid FincaId, string Codigo, string Nombre, string Municipio, string Vereda, bool ConsentimientoDatos);

/// <summary>Puerto del modelo de lectura.</summary>
public interface IGenealogiaReadModel
{
    Task<GenealogiaLoteDto?> ObtenerGenealogiaAsync(Guid loteId, CancellationToken ct);
    Task<IReadOnlyList<LoteResumenDto>> ListarLotesAsync(CancellationToken ct);
    Task<IReadOnlyList<FincaResumenDto>> ListarFincasAsync(CancellationToken ct);
}

/// <summary>RF4 — Consultar la genealogía del lote: insumo de la declaración de diligencia debida.</summary>
public sealed class ConsultarGenealogiaHandler(IGenealogiaReadModel lectura)
{
    public async Task<GenealogiaLoteDto> EjecutarAsync(Guid loteId, CancellationToken ct) =>
        await lectura.ObtenerGenealogiaAsync(loteId, ct)
        ?? throw new RecursoNoEncontradoException($"Lote {loteId} no encontrado.");
}
