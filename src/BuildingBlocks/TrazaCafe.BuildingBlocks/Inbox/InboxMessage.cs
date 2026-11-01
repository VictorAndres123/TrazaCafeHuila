namespace TrazaCafe.BuildingBlocks.Inbox;

/// <summary>
/// Registro de un mensaje ya procesado. La clave primaria (message_id) convierte la entrega
/// "al menos una vez" de RabbitMQ en efectos "exactamente una vez".
/// </summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; private set; }
    public string Tipo { get; private set; } = default!;
    public DateTimeOffset ProcesadoEn { get; private set; }

    private InboxMessage() { }

    public static InboxMessage Crear(Guid messageId, string tipo, DateTimeOffset ahora) =>
        new() { MessageId = messageId, Tipo = tipo, ProcesadoEn = ahora };
}
