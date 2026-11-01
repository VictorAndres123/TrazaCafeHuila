using TrazaCafe.Cumplimiento.Domain;

namespace TrazaCafe.Cumplimiento.Application;

/// <summary>
/// Puerto hacia el proveedor geoespacial (ADR-006). El adaptador solo informa pérdidas de bosque;
/// la regla de la fecha de corte la decide el dominio.
/// </summary>
public interface IVerificadorDeforestacion
{
    Task<IReadOnlyList<PerdidaDeBosque>> BuscarPerdidasAsync(string geometriaGeoJson, CancellationToken ct);
}

public interface IFincaReplicaRepository
{
    Task<FincaReplica?> ObtenerAsync(Guid fincaId, CancellationToken ct);
    Task<IReadOnlyList<FincaReplica>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    void Agregar(FincaReplica finca);
}

public interface IVerificacionRepository
{
    Task<bool> ExisteParaLoteAsync(Guid loteId, CancellationToken ct);
    void Agregar(VerificacionLote verificacion);
}

public interface IEventOutbox
{
    void Encolar<TEvento>(string tipo, TEvento evento) where TEvento : class;
}

public interface IInbox
{
    Task<bool> YaProcesadoAsync(Guid messageId, CancellationToken ct);
    void Registrar(Guid messageId, string tipo);
}

public interface IUnitOfWork
{
    Task GuardarCambiosAsync(CancellationToken ct);
}

/// <summary>
/// Error transitorio: llegó LoteConformado antes que alguna FincaRegistrada. Se reintenta (cola con TTL).
/// </summary>
public sealed class FincaNoReplicadaException(IReadOnlyCollection<Guid> fincas)
    : Exception($"Fincas aún no replicadas en Cumplimiento: {string.Join(", ", fincas)}.")
{
    public IReadOnlyCollection<Guid> Fincas { get; } = fincas;
}
