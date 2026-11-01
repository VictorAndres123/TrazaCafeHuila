namespace TrazaCafe.Cumplimiento.Domain;

/// <summary>Agregado: verificación de un lote. Bloqueado si alguna finca no es conforme.</summary>
public sealed class VerificacionLote
{
    private readonly List<ResultadoFinca> _resultados = [];

    public Guid LoteId { get; private set; }
    public bool Bloqueado { get; private set; }
    public DateTimeOffset VerificadoEn { get; private set; }
    public IReadOnlyCollection<ResultadoFinca> Resultados => _resultados.AsReadOnly();

    private VerificacionLote() { }

    public static VerificacionLote Crear(Guid loteId, IReadOnlyCollection<ResultadoFinca> resultados, DateTimeOffset ahora)
    {
        if (loteId == Guid.Empty) throw new DomainException("La verificación requiere el lote.");
        if (resultados.Count == 0) throw new DomainException("La verificación requiere al menos una finca.");
        if (resultados.Select(r => r.FincaId).Distinct().Count() != resultados.Count)
            throw new DomainException("Resultados repetidos para una misma finca.");

        var verificacion = new VerificacionLote
        {
            LoteId = loteId,
            Bloqueado = resultados.Any(r => !r.Conforme),
            VerificadoEn = ahora
        };
        verificacion._resultados.AddRange(resultados);
        return verificacion;
    }
}
