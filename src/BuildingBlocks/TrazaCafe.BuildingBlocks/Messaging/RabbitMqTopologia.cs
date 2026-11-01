using RabbitMQ.Client;

namespace TrazaCafe.BuildingBlocks.Messaging;

/// <summary>
/// Declara (de forma idempotente) el exchange de eventos y la cola del servicio con su cola de reintento
/// (TTL que devuelve el mensaje a la cola principal vía dead-lettering) y su DLQ.
/// </summary>
public static class RabbitMqTopologia
{
    public static async Task DeclararAsync(IChannel canal, string exchange, DefinicionConsumo? consumo, CancellationToken ct)
    {
        await canal.ExchangeDeclareAsync(
            exchange: exchange, type: ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        if (consumo is null) return;

        await canal.QueueDeclareAsync(
            queue: consumo.Cola, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct);

        foreach (var routingKey in consumo.RoutingKeys)
            await canal.QueueBindAsync(queue: consumo.Cola, exchange: exchange, routingKey: routingKey, cancellationToken: ct);

        var argumentosReintento = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = consumo.RetrasoReintentoMs,
            ["x-dead-letter-exchange"] = "",
            ["x-dead-letter-routing-key"] = consumo.Cola
        };
        await canal.QueueDeclareAsync(
            queue: consumo.ColaReintento, durable: true, exclusive: false, autoDelete: false,
            arguments: argumentosReintento, cancellationToken: ct);

        await canal.QueueDeclareAsync(
            queue: consumo.ColaDlq, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct);
    }
}
