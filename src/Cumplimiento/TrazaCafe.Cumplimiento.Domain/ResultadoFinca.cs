using System.Globalization;

namespace TrazaCafe.Cumplimiento.Domain;

/// <summary>Evaluación de una finca frente a la regla de la fecha de corte.</summary>
public sealed class ResultadoFinca
{
    public Guid FincaId { get; private set; }
    public bool Conforme { get; private set; }
    public string Detalle { get; private set; } = default!;

    private ResultadoFinca() { }

    public static ResultadoFinca Evaluar(Guid fincaId, IReadOnlyCollection<PerdidaDeBosque> perdidas)
    {
        if (fincaId == Guid.Empty) throw new DomainException("La evaluación requiere la finca.");

        var posteriores = perdidas.Where(p => ReglaEudr.EsPosteriorAlCorte(p.FechaPerdida)).ToList();
        var anteriores = perdidas.Where(p => !ReglaEudr.EsPosteriorAlCorte(p.FechaPerdida)).ToList();
        var corte = ReglaEudr.FechaDeCorte.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string detalle;
        if (posteriores.Count > 0)
            detalle = $"Pérdida de bosque posterior al {corte}: {Describir(posteriores)}.";
        else if (anteriores.Count > 0)
            detalle = $"Solo pérdida de bosque anterior a la fecha de corte {corte}: {Describir(anteriores)}.";
        else
            detalle = "Sin pérdida de bosque registrada en el área de la finca.";

        return new ResultadoFinca { FincaId = fincaId, Conforme = posteriores.Count == 0, Detalle = detalle };
    }

    private static string Describir(IEnumerable<PerdidaDeBosque> perdidas) =>
        string.Join(", ", perdidas.Select(p =>
            $"{p.AreaId} ({p.FechaPerdida.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})"));
}
