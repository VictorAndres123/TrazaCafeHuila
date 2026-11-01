namespace TrazaCafe.Contracts;

/// <summary>Una finca quedó registrada en Acopio; Cumplimiento mantiene una réplica de su geometría (ADR-003).</summary>
public sealed record FincaRegistradaV1(
    Guid FincaId,
    string Codigo,
    string GeometriaGeoJson,
    DateTimeOffset RegistradaEn);

/// <summary>Una entrega de café quedó registrada en un punto de acopio. Se conserva como diario de auditoría.</summary>
public sealed record EntregaRegistradaV1(
    Guid EntregaId,
    Guid FincaId,
    string PuntoAcopio,
    decimal Kilos,
    DateOnly FechaEntrega);

/// <summary>Se conformó un lote de exportación; debe verificarse contra áreas deforestadas.</summary>
public sealed record LoteConformadoV1(
    Guid LoteId,
    string Codigo,
    IReadOnlyList<Guid> FincaIds,
    decimal KilosTotales,
    DateTimeOffset ConformadoEn);

/// <summary>Resultado de la verificación de un lote, finca por finca.</summary>
public sealed record LoteVerificadoV1(
    Guid LoteId,
    IReadOnlyList<ResultadoFincaV1> Resultados,
    DateTimeOffset VerificadoEn);

public sealed record ResultadoFincaV1(Guid FincaId, bool Conforme, string Detalle);
