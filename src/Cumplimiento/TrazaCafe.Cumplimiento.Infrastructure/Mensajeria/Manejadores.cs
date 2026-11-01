using System.Text.Json;
using TrazaCafe.BuildingBlocks.Messaging;
using TrazaCafe.Contracts;
using TrazaCafe.Cumplimiento.Application;

namespace TrazaCafe.Cumplimiento.Infrastructure.Mensajeria;

internal sealed class FincaRegistradaManejador(RegistrarFincaReplicaHandler casoDeUso) : IManejadorMensaje
{
    public string TipoEvento => TiposDeEvento.FincaRegistrada;

    public async Task ManejarAsync(Guid messageId, ReadOnlyMemory<byte> cuerpo, CancellationToken ct)
    {
        var evento = JsonSerializer.Deserialize<FincaRegistradaV1>(cuerpo.Span, JsonContratos.Opciones)
                     ?? throw new JsonException("Cuerpo vacío.");
        await casoDeUso.EjecutarAsync(messageId, evento, ct);
    }
}

internal sealed class LoteConformadoManejador(VerificarLoteHandler casoDeUso) : IManejadorMensaje
{
    public string TipoEvento => TiposDeEvento.LoteConformado;

    public async Task ManejarAsync(Guid messageId, ReadOnlyMemory<byte> cuerpo, CancellationToken ct)
    {
        var evento = JsonSerializer.Deserialize<LoteConformadoV1>(cuerpo.Span, JsonContratos.Opciones)
                     ?? throw new JsonException("Cuerpo vacío.");
        await casoDeUso.EjecutarAsync(messageId, evento, ct);
    }
}
