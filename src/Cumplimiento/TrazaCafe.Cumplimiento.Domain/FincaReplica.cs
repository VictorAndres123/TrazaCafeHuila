namespace TrazaCafe.Cumplimiento.Domain;

/// <summary>
/// Réplica local de la geometría de una finca, alimentada por FincaRegistrada (ADR-003).
/// No guarda datos personales del caficultor (minimización de datos).
/// </summary>
public sealed class FincaReplica
{
    public Guid FincaId { get; private set; }
    public string Codigo { get; private set; } = default!;
    public string GeometriaGeoJson { get; private set; } = default!;
    public DateTimeOffset RecibidaEn { get; private set; }

    private FincaReplica() { }

    public static FincaReplica Crear(Guid fincaId, string codigo, string geometriaGeoJson, DateTimeOffset ahora)
    {
        if (fincaId == Guid.Empty) throw new DomainException("La réplica requiere la finca.");
        if (string.IsNullOrWhiteSpace(geometriaGeoJson)) throw new DomainException("La réplica requiere la geometría.");
        return new FincaReplica { FincaId = fincaId, Codigo = codigo, GeometriaGeoJson = geometriaGeoJson, RecibidaEn = ahora };
    }

    public void Actualizar(string codigo, string geometriaGeoJson, DateTimeOffset ahora)
    {
        Codigo = codigo;
        GeometriaGeoJson = geometriaGeoJson;
        RecibidaEn = ahora;
    }
}
