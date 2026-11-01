using TrazaCafe.Contracts;
using TrazaCafe.Cumplimiento.Domain;

namespace TrazaCafe.Cumplimiento.Application;

/// <summary>
/// RF3 — Verifica cada finca del lote contra pérdidas de bosque y publica LoteVerificado vía outbox.
/// </summary>
public sealed class VerificarLoteHandler(
    IFincaReplicaRepository fincas,
    IVerificacionRepository verificaciones,
    IVerificadorDeforestacion verificador,
    IEventOutbox outbox,
    IInbox inbox,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<bool> EjecutarAsync(Guid messageId, LoteConformadoV1 evento, CancellationToken ct)
    {
        if (await inbox.YaProcesadoAsync(messageId, ct)) return false;

        // Idempotencia de negocio: el mismo lote no se verifica dos veces aunque llegue con otro messageId.
        if (await verificaciones.ExisteParaLoteAsync(evento.LoteId, ct))
        {
            inbox.Registrar(messageId, TiposDeEvento.LoteConformado);
            await unitOfWork.GuardarCambiosAsync(ct);
            return false;
        }

        var replicas = await fincas.ObtenerPorIdsAsync(evento.FincaIds, ct);
        var faltantes = evento.FincaIds.Except(replicas.Select(f => f.FincaId)).ToList();
        if (faltantes.Count > 0)
            throw new FincaNoReplicadaException(faltantes);

        var resultados = new List<ResultadoFinca>(replicas.Count);
        foreach (var finca in replicas)
        {
            var perdidas = await verificador.BuscarPerdidasAsync(finca.GeometriaGeoJson, ct);
            resultados.Add(ResultadoFinca.Evaluar(finca.FincaId, perdidas));
        }

        var verificacion = VerificacionLote.Crear(evento.LoteId, resultados, reloj.GetUtcNow());
        verificaciones.Agregar(verificacion);

        outbox.Encolar(TiposDeEvento.LoteVerificado, new LoteVerificadoV1(
            verificacion.LoteId,
            verificacion.Resultados.Select(r => new ResultadoFincaV1(r.FincaId, r.Conforme, r.Detalle)).ToList(),
            verificacion.VerificadoEn));

        inbox.Registrar(messageId, TiposDeEvento.LoteConformado);
        await unitOfWork.GuardarCambiosAsync(ct);
        return true;
    }
}
