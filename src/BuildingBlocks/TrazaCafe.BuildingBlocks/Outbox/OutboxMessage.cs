using System.Text.Json;
using TrazaCafe.Contracts;

namespace TrazaCafe.BuildingBlocks.Outbox;

/// <summary>
/// Evento pendiente de publicar. Se inserta en la misma transacción que el cambio de negocio (ADR-002).
/// En el MVP no se purga: funciona también como diario de auditoría append-only.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset OcurridoEn { get; private set; }
    public DateTimeOffset? PublicadoEn { get; private set; }
    public int Intentos { get; private set; }
    public string? UltimoError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Crear<TEvento>(string tipo, TEvento evento, DateTimeOffset ahora) where TEvento : class =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Tipo = tipo,
            Payload = JsonSerializer.Serialize(evento, JsonContratos.Opciones),
            OcurridoEn = ahora
        };

    public void MarcarPublicado(DateTimeOffset ahora)
    {
        PublicadoEn = ahora;
        Intentos++;
        UltimoError = null;
    }

    public void RegistrarFallo(string error)
    {
        Intentos++;
        UltimoError = error.Length > 1000 ? error[..1000] : error;
    }
}
