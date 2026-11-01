using TrazaCafe.Acopio.Application.Errores;
using TrazaCafe.Acopio.Application.Puertos;
using TrazaCafe.Acopio.Domain;
using TrazaCafe.Contracts;

namespace TrazaCafe.Acopio.Application.Comandos;

/// <summary>
/// RF3 (lado Acopio) — Aplica el resultado de Cumplimiento al lote. Consumidor idempotente vía inbox.
/// </summary>
public sealed class AplicarVerificacionHandler(
    ILoteRepository lotes,
    IInbox inbox,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    /// <returns><c>false</c> si el mensaje ya había sido procesado (sin efectos).</returns>
    public async Task<bool> EjecutarAsync(Guid messageId, LoteVerificadoV1 evento, CancellationToken ct)
    {
        if (await inbox.YaProcesadoAsync(messageId, ct))
            return false;

        var lote = await lotes.ObtenerAsync(evento.LoteId, ct)
                   ?? throw new RecursoNoEncontradoException($"Lote {evento.LoteId} no encontrado.");

        lote.AplicarVerificacion(
            evento.Resultados.Select(r => new ResultadoVerificacionFinca(r.FincaId, r.Conforme, r.Detalle)).ToList(),
            reloj.GetUtcNow());

        inbox.Registrar(messageId, TiposDeEvento.LoteVerificado);
        await unitOfWork.GuardarCambiosAsync(ct);
        return true;
    }
}
