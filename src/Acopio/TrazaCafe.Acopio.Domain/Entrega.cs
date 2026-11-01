using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TrazaCafe.Acopio.Domain;

/// <summary>
/// Entrega de café pergamino seco de una finca en un punto de acopio.
/// El identificador lo genera el cliente (posiblemente sin conexión), lo que permite
/// sincronizaciones idempotentes (ADR-005).
/// </summary>
public sealed class Entrega
{
    public const decimal MaxKilosPorEntrega = 10_000m;

    public Guid Id { get; private set; }
    public Guid FincaId { get; private set; }
    public string PuntoAcopio { get; private set; } = default!;
    public decimal Kilos { get; private set; }
    public DateOnly FechaEntrega { get; private set; }

    /// <summary>SHA-256 de la representación canónica: detecta reenvíos con contenido alterado.</summary>
    public string Hash { get; private set; } = default!;

    /// <summary>Lote al que fue asignada. Una entrega pertenece como máximo a un lote (invariante I2).</summary>
    public Guid? LoteId { get; private set; }

    public DateTimeOffset RegistradaEn { get; private set; }

    public bool EstaAsignada => LoteId.HasValue;

    private Entrega() { }

    public static Entrega Registrar(
        Guid id, Guid fincaId, string puntoAcopio, decimal kilos, DateOnly fechaEntrega, DateTimeOffset ahora)
    {
        if (id == Guid.Empty)
            throw new DomainException("La entrega debe traer un identificador generado por el cliente.");
        if (fincaId == Guid.Empty)
            throw new DomainException($"La entrega {id} no indica la finca de origen.");
        if (string.IsNullOrWhiteSpace(puntoAcopio))
            throw new DomainException($"La entrega {id} no indica el punto de acopio.");
        if (kilos <= 0)
            throw new DomainException($"La entrega {id} debe tener kilos mayores que cero.");
        if (kilos > MaxKilosPorEntrega)
            throw new DomainException($"La entrega {id} supera el máximo de {MaxKilosPorEntrega} kg por entrega.");
        if (decimal.Round(kilos, 3) != kilos)
            throw new DomainException($"La entrega {id} admite como máximo tres decimales en los kilos.");
        if (fechaEntrega > DateOnly.FromDateTime(ahora.UtcDateTime))
            throw new DomainException($"La entrega {id} tiene una fecha futura.");

        var punto = puntoAcopio.Trim();
        return new Entrega
        {
            Id = id,
            FincaId = fincaId,
            PuntoAcopio = punto,
            Kilos = kilos,
            FechaEntrega = fechaEntrega,
            Hash = CalcularHash(id, fincaId, punto, kilos, fechaEntrega),
            RegistradaEn = ahora
        };
    }

    /// <summary>
    /// Hash de la representación canónica. Es estable frente a variaciones de formato
    /// (12.5 y 12.500 producen el mismo hash; mayúsculas y espacios del punto de acopio se normalizan).
    /// </summary>
    public static string CalcularHash(Guid id, Guid fincaId, string puntoAcopio, decimal kilos, DateOnly fecha)
    {
        var canonico = string.Join('|',
            id.ToString("N"),
            fincaId.ToString("N"),
            puntoAcopio.Trim().ToUpperInvariant(),
            kilos.ToString("0.000", CultureInfo.InvariantCulture),
            fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
    }

    /// <summary>Solo el agregado <see cref="Lote"/> puede asignar una entrega.</summary>
    internal void AsignarA(Guid loteId)
    {
        if (LoteId.HasValue)
            throw new DomainException($"La entrega {Id} ya pertenece al lote {LoteId}.");
        LoteId = loteId;
    }
}
