using TrazaCafe.Cumplimiento.Domain;

namespace TrazaCafe.Cumplimiento.Tests;

public sealed class ReglaEudrTests
{
    private static readonly Guid Finca = Guid.NewGuid();

    [Theory(DisplayName = "Fecha de corte 31-12-2020: solo la pérdida POSTERIOR hace no conforme")]
    [InlineData("2019-08-01", true)]
    [InlineData("2020-12-31", true)]
    [InlineData("2021-01-01", false)]
    [InlineData("2023-05-10", false)]
    public void Fecha_de_corte(string fechaPerdida, bool conformeEsperado)
    {
        var resultado = ResultadoFinca.Evaluar(Finca, [new PerdidaDeBosque("DEF-X", DateOnly.Parse(fechaPerdida))]);

        Assert.Equal(conformeEsperado, resultado.Conforme);
    }

    [Fact(DisplayName = "Sin pérdidas de bosque → conforme")]
    public void Sin_perdidas_es_conforme()
    {
        var resultado = ResultadoFinca.Evaluar(Finca, []);

        Assert.True(resultado.Conforme);
        Assert.Contains("Sin pérdida", resultado.Detalle);
    }

    [Fact(DisplayName = "Una verificación con alguna finca no conforme queda bloqueada")]
    public void Verificacion_con_una_no_conforme_queda_bloqueada()
    {
        var ok = ResultadoFinca.Evaluar(Guid.NewGuid(), []);
        var mal = ResultadoFinca.Evaluar(Guid.NewGuid(), [new PerdidaDeBosque("DEF-2023-001", new DateOnly(2023, 5, 10))]);

        var verificacion = VerificacionLote.Crear(Guid.NewGuid(), [ok, mal], DateTimeOffset.UtcNow);

        Assert.True(verificacion.Bloqueado);
        Assert.Contains("DEF-2023-001", mal.Detalle);
    }
}
