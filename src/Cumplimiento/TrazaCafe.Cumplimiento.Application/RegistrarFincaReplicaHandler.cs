using TrazaCafe.Contracts;
using TrazaCafe.Cumplimiento.Domain;

namespace TrazaCafe.Cumplimiento.Application;

/// <summary>Mantiene la réplica local de fincas a partir de FincaRegistrada (consumidor idempotente).</summary>
public sealed class RegistrarFincaReplicaHandler(
    IFincaReplicaRepository fincas, IInbox inbox, IUnitOfWork unitOfWork, TimeProvider reloj)
{
    public async Task<bool> EjecutarAsync(Guid messageId, FincaRegistradaV1 evento, CancellationToken ct)
    {
        if (await inbox.YaProcesadoAsync(messageId, ct)) return false;

        var ahora = reloj.GetUtcNow();
        var existente = await fincas.ObtenerAsync(evento.FincaId, ct);
        if (existente is null)
            fincas.Agregar(FincaReplica.Crear(evento.FincaId, evento.Codigo, evento.GeometriaGeoJson, ahora));
        else
            existente.Actualizar(evento.Codigo, evento.GeometriaGeoJson, ahora);

        inbox.Registrar(messageId, TiposDeEvento.FincaRegistrada);
        await unitOfWork.GuardarCambiosAsync(ct);
        return true;
    }
}
