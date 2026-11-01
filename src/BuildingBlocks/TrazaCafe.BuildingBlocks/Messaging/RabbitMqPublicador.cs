using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace TrazaCafe.BuildingBlocks.Messaging;

public interface IPublicadorMensajes
{
    /// <summary>Publica un evento en el exchange topic usando su tipo como routing key. Espera el confirm del broker.</summary>
    Task PublicarEventoAsync(MensajeSaliente mensaje, CancellationToken ct);

    /// <summary>Publica directamente en una cola (reintento o DLQ) conservando el messageId.</summary>
    Task PublicarEnColaAsync(string cola, MensajeSaliente mensaje, int intentos, string? error, CancellationToken ct);
}

/// <summary>Publicador con publisher confirms y mensajes persistentes.</summary>
public sealed class RabbitMqPublicador(
    RabbitMqConexion conexion,
    IOptions<RabbitMqOptions> opciones,
    ILogger<RabbitMqPublicador> log) : IPublicadorMensajes, IAsyncDisposable
{
    public const string CabeceraIntentos = "x-intentos";
    public const string CabeceraError = "x-ultimo-error";

    private readonly SemaphoreSlim _candado = new(1, 1);
    private IChannel? _canal;
    private bool _topologiaDeclarada;

    public Task PublicarEventoAsync(MensajeSaliente mensaje, CancellationToken ct) =>
        PublicarAsync(opciones.Value.Exchange, mensaje.Tipo, mensaje, cabeceras: null, ct);

    public Task PublicarEnColaAsync(string cola, MensajeSaliente mensaje, int intentos, string? error, CancellationToken ct)
    {
        var cabeceras = new Dictionary<string, object?> { [CabeceraIntentos] = intentos };
        if (error is not null) cabeceras[CabeceraError] = error.Length > 500 ? error[..500] : error;
        return PublicarAsync(exchange: "", routingKey: cola, mensaje, cabeceras, ct);
    }

    private async Task PublicarAsync(
        string exchange, string routingKey, MensajeSaliente mensaje, IDictionary<string, object?>? cabeceras, CancellationToken ct)
    {
        await _candado.WaitAsync(ct);
        try
        {
            var canal = await ObtenerCanalAsync(ct);
            var propiedades = new BasicProperties
            {
                MessageId = mensaje.MessageId.ToString(),
                Type = mensaje.Tipo,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(mensaje.OcurridoEn.ToUnixTimeSeconds()),
                Headers = cabeceras
            };

            // Con confirmaciones habilitadas, esta llamada espera el ack del broker (lanza excepción si hay nack).
            await canal.BasicPublishAsync(exchange, routingKey, mandatory: false, basicProperties: propiedades,
                body: mensaje.Cuerpo, cancellationToken: ct);

            log.LogDebug("Publicado {Tipo} {MessageId} en '{Exchange}' ({RoutingKey})",
                mensaje.Tipo, mensaje.MessageId, exchange, routingKey);
        }
        finally
        {
            _candado.Release();
        }
    }

    private async Task<IChannel> ObtenerCanalAsync(CancellationToken ct)
    {
        if (_canal is { IsOpen: true }) return _canal;

        var cn = await conexion.ObtenerAsync(ct);
        _canal = await cn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        if (!_topologiaDeclarada)
        {
            foreach (var cola in TopologiaTrazaCafe.Todas)
                await RabbitMqTopologia.DeclararAsync(_canal, opciones.Value.Exchange, cola, ct);
            _topologiaDeclarada = true;
        }
        return _canal;
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null)
        {
            try { await _canal.CloseAsync(); } catch { /* best-effort */ }
            await _canal.DisposeAsync();
        }
        _candado.Dispose();
    }
}
