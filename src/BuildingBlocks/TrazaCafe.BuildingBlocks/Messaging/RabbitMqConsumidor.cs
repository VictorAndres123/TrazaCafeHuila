using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TrazaCafe.BuildingBlocks.Messaging;

/// <summary>Adaptador de entrada para un tipo de evento. Cada servicio registra los suyos.</summary>
public interface IManejadorMensaje
{
    string TipoEvento { get; }
    Task ManejarAsync(Guid messageId, ReadOnlyMemory<byte> cuerpo, CancellationToken ct);
}

/// <summary>
/// Consume la cola del servicio con ack manual. Ante error: reintento diferido (cola .retry con TTL) hasta
/// <see cref="DefinicionConsumo.MaxIntentos"/>; después, DLQ. Los cuerpos inválidos van directo a la DLQ.
/// </summary>
public sealed class RabbitMqConsumidor(
    RabbitMqConexion conexion,
    IPublicadorMensajes publicador,
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> opciones,
    DefinicionConsumo definicion,
    ILogger<RabbitMqConsumidor> log) : BackgroundService
{
    private IChannel? _canal;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var cn = await conexion.ObtenerAsync(stoppingToken);
            _canal = await cn.CreateChannelAsync(cancellationToken: stoppingToken);

            await RabbitMqTopologia.DeclararAsync(_canal, opciones.Value.Exchange, definicion, stoppingToken);
            await _canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

            var consumidor = new AsyncEventingBasicConsumer(_canal);
            consumidor.ReceivedAsync += (_, entrega) => ProcesarAsync(entrega, stoppingToken);

            await _canal.BasicConsumeAsync(queue: definicion.Cola, autoAck: false, consumer: consumidor,
                cancellationToken: stoppingToken);

            log.LogInformation("Consumiendo '{Cola}' ({RoutingKeys})", definicion.Cola, string.Join(", ", definicion.RoutingKeys));
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado normal
        }
    }

    private async Task ProcesarAsync(BasicDeliverEventArgs entrega, CancellationToken ct)
    {
        var canal = _canal!;
        var tipo = entrega.BasicProperties.Type ?? entrega.RoutingKey;
        var cuerpo = entrega.Body.ToArray(); // copiar: el buffer solo es válido durante el callback
        Guid.TryParse(entrega.BasicProperties.MessageId, out var messageId);
        var intentosPrevios = LeerIntentos(entrega.BasicProperties.Headers);

        using var _ = log.BeginScope(new Dictionary<string, object> { ["MessageId"] = messageId, ["Tipo"] = tipo });

        try
        {
            if (messageId == Guid.Empty)
                throw new JsonException("El mensaje no trae un messageId válido.");

            await using var scope = scopes.CreateAsyncScope();
            var manejador = scope.ServiceProvider.GetServices<IManejadorMensaje>().FirstOrDefault(m => m.TipoEvento == tipo);
            if (manejador is null)
            {
                log.LogWarning("Sin manejador para {Tipo}; se descarta", tipo);
            }
            else
            {
                await manejador.ManejarAsync(messageId, cuerpo, ct);
                log.LogInformation("Procesado {Tipo} {MessageId}", tipo, messageId);
            }

            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var intentos = intentosPrevios + 1;
            var noRecuperable = ex is JsonException;
            var destino = noRecuperable || intentos >= definicion.MaxIntentos ? definicion.ColaDlq : definicion.ColaReintento;

            log.LogWarning("Fallo procesando {Tipo} {MessageId} (intento {Intento}/{Max}) → {Destino}: {Error}",
                tipo, messageId, intentos, definicion.MaxIntentos, destino, ex.Message);

            try
            {
                var mensaje = new MensajeSaliente(messageId, tipo, cuerpo, DateTimeOffset.UtcNow);
                await publicador.PublicarEnColaAsync(destino, mensaje, intentos, ex.Message, ct);
                await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            catch (Exception exReenvio) when (exReenvio is not OperationCanceledException)
            {
                log.LogError(exReenvio, "No se pudo enviar a {Destino}; se devuelve a la cola", destino);
                await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
            }
        }
    }

    private static int LeerIntentos(IDictionary<string, object?>? cabeceras)
    {
        if (cabeceras is null || !cabeceras.TryGetValue(RabbitMqPublicador.CabeceraIntentos, out var valor) || valor is null)
            return 0;

        return valor switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var n) => n,
            _ => int.TryParse(valor.ToString(), out var m) ? m : 0
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_canal is not null)
        {
            try { await _canal.CloseAsync(cancellationToken); } catch { /* best-effort */ }
            await _canal.DisposeAsync();
        }
    }
}
