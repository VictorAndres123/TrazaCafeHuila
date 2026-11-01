using System.Text.Json;
using TrazaCafe.Acopio.Application.Comandos;
using TrazaCafe.BuildingBlocks.Messaging;
using TrazaCafe.Contracts;

namespace TrazaCafe.Acopio.Infrastructure.Mensajeria;

/// <summary>Adaptador de entrada: traduce el mensaje AMQP al caso de uso AplicarVerificacion.</summary>
internal sealed class LoteVerificadoManejador(AplicarVerificacionHandler casoDeUso) : IManejadorMensaje
{
    public string TipoEvento => TiposDeEvento.LoteVerificado;

    public async Task ManejarAsync(Guid messageId, ReadOnlyMemory<byte> cuerpo, CancellationToken ct)
    {
        var evento = JsonSerializer.Deserialize<LoteVerificadoV1>(cuerpo.Span, JsonContratos.Opciones)
                     ?? throw new JsonException("Cuerpo vacío.");
        await casoDeUso.EjecutarAsync(messageId, evento, ct);
    }
}
