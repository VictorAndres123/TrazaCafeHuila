using TrazaCafe.Cumplimiento.Infrastructure.Geoespacial;

namespace TrazaCafe.Cumplimiento.Tests;

/// <summary>Adaptador geoespacial real (NetTopologySuite) con una capa mínima en memoria.</summary>
public sealed class NtsVerificadorTests
{
    private const string Capa = """
        {"type":"FeatureCollection","features":[
          {"type":"Feature","properties":{"id":"DEF-2023-001","fecha_perdida":"2023-05-10"},
           "geometry":{"type":"Polygon","coordinates":[[[-75.895,1.805],[-75.885,1.805],[-75.885,1.815],[-75.895,1.815],[-75.895,1.805]]]}},
          {"type":"Feature","properties":{"id":"DEF-2019-001","fecha_perdida":"2019-08-01"},
           "geometry":{"type":"Polygon","coordinates":[[[-75.895,2.385],[-75.880,2.385],[-75.880,2.395],[-75.895,2.395],[-75.895,2.385]]]}}
        ]}
        """;

    private const string FincaElMirador =
        """{"type":"Polygon","coordinates":[[[-75.900,1.800],[-75.890,1.800],[-75.890,1.810],[-75.900,1.810],[-75.900,1.800]]]}""";

    private const string FincaLaEsperanza =
        """{"type":"Polygon","coordinates":[[[-76.060,1.840],[-76.050,1.840],[-76.050,1.850],[-76.060,1.850],[-76.060,1.840]]]}""";

    private readonly NtsVerificadorDeforestacion _verificador = new(Capa);

    [Fact(DisplayName = "NTS · Detecta la intersección de la finca con un área deforestada")]
    public async Task Detecta_interseccion()
    {
        var perdidas = await _verificador.BuscarPerdidasAsync(FincaElMirador, CancellationToken.None);

        var perdida = Assert.Single(perdidas);
        Assert.Equal("DEF-2023-001", perdida.AreaId);
        Assert.Equal(new DateOnly(2023, 5, 10), perdida.FechaPerdida);
    }

    [Fact(DisplayName = "NTS · Una finca lejos de las áreas no reporta pérdidas")]
    public async Task Sin_interseccion()
    {
        var perdidas = await _verificador.BuscarPerdidasAsync(FincaLaEsperanza, CancellationToken.None);

        Assert.Empty(perdidas);
        Assert.Equal(2, _verificador.AreasCargadas);
    }
}
