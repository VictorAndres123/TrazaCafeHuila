using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO.Converters;
using TrazaCafe.Cumplimiento.Application;
using TrazaCafe.Cumplimiento.Domain;

namespace TrazaCafe.Cumplimiento.Infrastructure.Geoespacial;

public sealed class GeodataOptions
{
    public const string Seccion = "Geodata";
    public string AreasDeforestadasPath { get; set; } = "geodata/areas_deforestadas.geojson";
}

/// <summary>
/// Adaptador geoespacial simulado (ADR-006): carga en memoria un GeoJSON de áreas con pérdida de bosque,
/// las indexa en un STRtree y evalúa intersecciones con NetTopologySuite.
/// Reemplazable por PostGIS (ST_Intersects) o una API externa sin tocar dominio ni aplicación.
/// </summary>
public sealed class NtsVerificadorDeforestacion : IVerificadorDeforestacion
{
    private readonly STRtree<AreaDeforestada> _indice = new();
    private readonly JsonSerializerOptions _geoJson;

    public int AreasCargadas { get; }

    public NtsVerificadorDeforestacion(IOptions<GeodataOptions> opciones, ILogger<NtsVerificadorDeforestacion> log)
        : this(File.ReadAllText(ResolverRuta(opciones.Value.AreasDeforestadasPath)), log)
    {
    }

    internal NtsVerificadorDeforestacion(string featureCollectionGeoJson, ILogger? log = null)
    {
        _geoJson = new JsonSerializerOptions();
        _geoJson.Converters.Add(new GeoJsonConverterFactory());

        using var documento = JsonDocument.Parse(featureCollectionGeoJson);
        foreach (var feature in documento.RootElement.GetProperty("features").EnumerateArray())
        {
            var propiedades = feature.GetProperty("properties");
            var id = propiedades.GetProperty("id").GetString()
                     ?? throw new InvalidDataException("Área sin id.");
            var fecha = DateOnly.ParseExact(propiedades.GetProperty("fecha_perdida").GetString()!, "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
            var geometria = LeerGeometria(feature.GetProperty("geometry").GetRawText());

            _indice.Insert(geometria.EnvelopeInternal, new AreaDeforestada(id, fecha, geometria));
            AreasCargadas++;
        }
        _indice.Build();

        (log ?? NullLogger.Instance).LogInformation("Verificador NTS: {Areas} áreas con pérdida de bosque indexadas", AreasCargadas);
    }

    public Task<IReadOnlyList<PerdidaDeBosque>> BuscarPerdidasAsync(string geometriaGeoJson, CancellationToken ct)
    {
        var finca = LeerGeometria(geometriaGeoJson);

        IReadOnlyList<PerdidaDeBosque> perdidas = _indice.Query(finca.EnvelopeInternal) // filtro grueso por envolvente
            .Where(area => area.Geometria.Intersects(finca))                               // filtro exacto
            .Select(area => new PerdidaDeBosque(area.Id, area.FechaPerdida))
            .OrderBy(p => p.FechaPerdida)
            .ToList();

        return Task.FromResult(perdidas);
    }

    private Geometry LeerGeometria(string geoJson)
    {
        var geometria = JsonSerializer.Deserialize<Geometry>(geoJson, _geoJson)
                        ?? throw new InvalidDataException("Geometría GeoJSON vacía.");
        if (!geometria.IsValid)
            throw new InvalidDataException("Geometría GeoJSON inválida (auto-intersección o anillo abierto).");
        return geometria;
    }

    private static string ResolverRuta(string ruta) =>
        Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);

    private sealed record AreaDeforestada(string Id, DateOnly FechaPerdida, Geometry Geometria);
}
