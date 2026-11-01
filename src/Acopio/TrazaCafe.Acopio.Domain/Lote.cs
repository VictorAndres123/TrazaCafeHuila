namespace TrazaCafe.Acopio.Domain;

/// <summary>
/// Agregado raíz: lote de exportación conformado a partir de entregas.
/// Invariantes:
///  I1. Tiene al menos una entrega.
///  I2. Una entrega pertenece como máximo a un lote.
///  I3. Kilos del lote = suma de los kilos de sus entregas (balance de masa).
///  I4. Solo se transita desde PendienteVerificacion; los estados finales son inmutables.
///  I5. Bloqueado si al menos una finca no es conforme; el resultado debe cubrir exactamente las fincas del lote.
/// Además: solo se incluyen fincas cuyo caficultor autorizó el uso de sus datos.
/// </summary>
public sealed class Lote
{
    private readonly List<LoteEntrega> _entregas = [];
    private readonly List<VerificacionFinca> _verificaciones = [];

    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = default!;
    public EstadoLote Estado { get; private set; }
    public decimal KilosTotales { get; private set; }
    public DateTimeOffset ConformadoEn { get; private set; }
    public DateTimeOffset? VerificadoEn { get; private set; }

    public IReadOnlyCollection<LoteEntrega> Entregas => _entregas.AsReadOnly();
    public IReadOnlyCollection<VerificacionFinca> Verificaciones => _verificaciones.AsReadOnly();
    public IReadOnlyCollection<Guid> FincaIds => _entregas.Select(e => e.FincaId).Distinct().ToList();
    public bool EstaFinalizado => Estado != EstadoLote.PendienteVerificacion;

    private Lote() { }

    public static Lote Conformar(
        Guid id,
        IReadOnlyCollection<Entrega> entregas,
        IReadOnlySet<Guid> fincasConConsentimiento,
        DateTimeOffset ahora)
    {
        if (id == Guid.Empty)
            throw new DomainException("El lote requiere un identificador.");

        // I1
        if (entregas is null || entregas.Count == 0)
            throw new DomainException("Un lote debe tener al menos una entrega.");

        var repetidas = entregas.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidas.Count > 0)
            throw new DomainException($"Entregas repetidas en la solicitud: {string.Join(", ", repetidas)}.");

        // I2
        var asignadas = entregas.Where(e => e.EstaAsignada).ToList();
        if (asignadas.Count > 0)
            throw new DomainException(
                $"Entregas que ya pertenecen a otro lote: {string.Join(", ", asignadas.Select(e => e.Id))}.");

        // Propiedad de los datos del caficultor
        var sinConsentimiento = entregas.Select(e => e.FincaId).Distinct()
            .Where(f => !fincasConConsentimiento.Contains(f)).ToList();
        if (sinConsentimiento.Count > 0)
            throw new DomainException(
                $"Fincas sin autorización de uso de datos: {string.Join(", ", sinConsentimiento)}.");

        var lote = new Lote
        {
            Id = id,
            Codigo = $"LOT-{ahora.UtcDateTime:yyyyMMdd}-{id.ToString("N")[^6..].ToUpperInvariant()}",
            Estado = EstadoLote.PendienteVerificacion,
            ConformadoEn = ahora
        };

        foreach (var entrega in entregas)
        {
            entrega.AsignarA(lote.Id);
            lote._entregas.Add(new LoteEntrega(entrega.Id, entrega.FincaId, entrega.Kilos));
        }

        // I3: balance de masa
        lote.KilosTotales = lote._entregas.Sum(e => e.Kilos);
        return lote;
    }

    public void AplicarVerificacion(IReadOnlyCollection<ResultadoVerificacionFinca> resultados, DateTimeOffset ahora)
    {
        // I4
        if (EstaFinalizado)
            throw new DomainException($"El lote {Codigo} ya fue verificado (estado {Estado}).");

        if (resultados is null || resultados.Count == 0)
            throw new DomainException("La verificación no trae resultados.");

        var fincasDelResultado = resultados.Select(r => r.FincaId).ToList();
        if (fincasDelResultado.Distinct().Count() != fincasDelResultado.Count)
            throw new DomainException("La verificación trae resultados repetidos para una misma finca.");

        // I5: el resultado debe referirse exactamente a las fincas del lote
        var fincasDelLote = FincaIds.ToHashSet();
        var ajenas = fincasDelResultado.Where(f => !fincasDelLote.Contains(f)).ToList();
        if (ajenas.Count > 0)
            throw new DomainException($"La verificación incluye fincas ajenas al lote: {string.Join(", ", ajenas)}.");

        var faltantes = fincasDelLote.Where(f => !fincasDelResultado.Contains(f)).ToList();
        if (faltantes.Count > 0)
            throw new DomainException($"La verificación no cubre las fincas: {string.Join(", ", faltantes)}.");

        foreach (var r in resultados)
            _verificaciones.Add(new VerificacionFinca(r.FincaId, r.Conforme, r.Detalle));

        Estado = resultados.All(r => r.Conforme) ? EstadoLote.Declarable : EstadoLote.Bloqueado;
        VerificadoEn = ahora;
    }
}

/// <summary>Composición del lote: qué entrega, de qué finca y con cuántos kilos (genealogía inmutable).</summary>
public sealed class LoteEntrega
{
    public Guid EntregaId { get; private set; }
    public Guid FincaId { get; private set; }
    public decimal Kilos { get; private set; }

    private LoteEntrega() { }

    internal LoteEntrega(Guid entregaId, Guid fincaId, decimal kilos)
    {
        EntregaId = entregaId;
        FincaId = fincaId;
        Kilos = kilos;
    }
}

/// <summary>Resultado de la verificación de una finca dentro del lote.</summary>
public sealed class VerificacionFinca
{
    public Guid FincaId { get; private set; }
    public bool Conforme { get; private set; }
    public string Detalle { get; private set; } = default!;

    private VerificacionFinca() { }

    internal VerificacionFinca(Guid fincaId, bool conforme, string detalle)
    {
        FincaId = fincaId;
        Conforme = conforme;
        Detalle = detalle;
    }
}

public sealed record ResultadoVerificacionFinca(Guid FincaId, bool Conforme, string Detalle);
