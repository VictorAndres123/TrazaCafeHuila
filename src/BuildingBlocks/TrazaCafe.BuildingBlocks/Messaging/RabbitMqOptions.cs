namespace TrazaCafe.BuildingBlocks.Messaging;

public sealed class RabbitMqOptions
{
    public const string Seccion = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Usuario { get; set; } = "guest";
    public string Clave { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    /// <summary>Exchange topic donde se publican todos los eventos de integración.</summary>
    public string Exchange { get; set; } = "trazacafe.events";

    public string NombreCliente { get; set; } = "trazacafe";
}

public sealed class OutboxOptions
{
    public const string Seccion = "Outbox";

    public int IntervaloMs { get; set; } = 1000;
    public int TamanoLote { get; set; } = 50;
}

/// <summary>Cola propia del servicio, sus bindings y la política de reintentos.</summary>
public sealed record DefinicionConsumo(
    string Cola,
    IReadOnlyList<string> RoutingKeys,
    int MaxIntentos = 5,
    int RetrasoReintentoMs = 5000)
{
    public string ColaReintento => $"{Cola}.retry";
    public string ColaDlq => $"{Cola}.dlq";
}

/// <summary>Mensaje listo para salir hacia el broker.</summary>
public sealed record MensajeSaliente(Guid MessageId, string Tipo, ReadOnlyMemory<byte> Cuerpo, DateTimeOffset OcurridoEn);
