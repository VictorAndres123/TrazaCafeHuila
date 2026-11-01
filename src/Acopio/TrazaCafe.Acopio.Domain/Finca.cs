namespace TrazaCafe.Acopio.Domain;

/// <summary>
/// Finca cafetera georreferenciada. En el MVP se carga por seed; el polígono se guarda como GeoJSON (WGS84).
/// </summary>
public sealed class Finca
{
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = default!;
    public string Nombre { get; private set; } = default!;
    public string Municipio { get; private set; } = default!;
    public string Vereda { get; private set; } = default!;
    public string GeometriaGeoJson { get; private set; } = default!;

    /// <summary>
    /// La información de la finca es del caficultor: solo puede compartirse con el exportador
    /// si este autorizó expresamente el uso de sus datos.
    /// </summary>
    public bool ConsentimientoDatos { get; private set; }

    public DateTimeOffset RegistradaEn { get; private set; }

    private Finca() { }

    public static Finca Registrar(
        Guid id, string codigo, string nombre, string municipio, string vereda,
        string geometriaGeoJson, bool consentimientoDatos, DateTimeOffset ahora)
    {
        if (id == Guid.Empty) throw new DomainException("La finca requiere un identificador.");
        if (string.IsNullOrWhiteSpace(codigo)) throw new DomainException("La finca requiere un código.");
        if (string.IsNullOrWhiteSpace(geometriaGeoJson)) throw new DomainException("La finca requiere su polígono.");

        return new Finca
        {
            Id = id,
            Codigo = codigo.Trim(),
            Nombre = nombre.Trim(),
            Municipio = municipio.Trim(),
            Vereda = vereda.Trim(),
            GeometriaGeoJson = geometriaGeoJson,
            ConsentimientoDatos = consentimientoDatos,
            RegistradaEn = ahora
        };
    }
}
