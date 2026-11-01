using TrazaCafe.Acopio.Domain;

namespace TrazaCafe.Acopio.Domain.Tests;

internal static class Datos
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 31, 15, 0, 0, TimeSpan.Zero);
    public static readonly Guid FincaA = Guid.Parse("f1000000-0000-4000-8000-000000000001");
    public static readonly Guid FincaB = Guid.Parse("f1000000-0000-4000-8000-000000000002");
    public static readonly Guid FincaSinConsentimiento = Guid.Parse("f1000000-0000-4000-8000-000000000006");

    public static readonly IReadOnlySet<Guid> Consentidas = new HashSet<Guid> { FincaA, FincaB };

    public static Entrega Entrega(Guid finca, decimal kilos, Guid? id = null) =>
        Domain.Entrega.Registrar(id ?? Guid.NewGuid(), finca, "Acopio Pitalito", kilos, new DateOnly(2026, 10, 20), Ahora);

    public static Lote Lote(params Entrega[] entregas) =>
        Domain.Lote.Conformar(Guid.NewGuid(), entregas, Consentidas, Ahora);
}
