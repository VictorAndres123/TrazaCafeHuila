using TrazaCafe.Acopio.Domain;
using static TrazaCafe.Acopio.Domain.Tests.Datos;

namespace TrazaCafe.Acopio.Domain.Tests;

public sealed class EntregaTests
{
    [Theory(DisplayName = "Kilos fuera de rango son rechazados")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10000.5)]
    [InlineData(12.3456)]
    public void Kilos_invalidos_son_rechazados(double kilos)
    {
        Assert.Throws<DomainException>(() => Entrega(FincaA, (decimal)kilos));
    }

    [Fact(DisplayName = "Una entrega con fecha futura es rechazada")]
    public void Fecha_futura_es_rechazada()
    {
        Assert.Throws<DomainException>(() =>
            Domain.Entrega.Registrar(Guid.NewGuid(), FincaA, "Acopio", 10m, new DateOnly(2026, 11, 2), Ahora));
    }

    [Fact(DisplayName = "Hash canónico: estable frente a formato (12.5 = 12.500, mayúsculas/espacios)")]
    public void Hash_es_estable_frente_a_formato()
    {
        var id = Guid.NewGuid();
        var fecha = new DateOnly(2026, 10, 20);

        var h1 = Domain.Entrega.CalcularHash(id, FincaA, "Acopio Pitalito", 12.5m, fecha);
        var h2 = Domain.Entrega.CalcularHash(id, FincaA, "  ACOPIO PITALITO ", 12.500m, fecha);

        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length);
    }

    [Fact(DisplayName = "Hash canónico: cambia si cambia el contenido (detecta manipulación)")]
    public void Hash_cambia_si_cambian_los_kilos()
    {
        var id = Guid.NewGuid();
        var fecha = new DateOnly(2026, 10, 20);

        var original = Domain.Entrega.CalcularHash(id, FincaA, "Acopio Pitalito", 12.5m, fecha);
        var alterada = Domain.Entrega.CalcularHash(id, FincaA, "Acopio Pitalito", 125m, fecha);

        Assert.NotEqual(original, alterada);
    }
}
