using TrazaCafe.Acopio.Domain;

namespace TrazaCafe.Acopio.Application.Puertos;

// ---- Puertos de salida del lado de escritura (comandos) ----

public interface IFincaRepository
{
    Task<IReadOnlyList<Finca>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}

public interface IEntregaRepository
{
    /// <summary>Hashes de las entregas ya registradas, para decidir duplicado (200) o conflicto (409).</summary>
    Task<IReadOnlyDictionary<Guid, string>> ObtenerHashesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task<IReadOnlyList<Entrega>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    void Agregar(Entrega entrega);
}

public interface ILoteRepository
{
    Task<Lote?> ObtenerAsync(Guid id, CancellationToken ct);
    void Agregar(Lote lote);
}

/// <summary>
/// Transactional Outbox (ADR-002): el evento se guarda en la MISMA transacción que el cambio de negocio.
/// </summary>
public interface IEventOutbox
{
    void Encolar<TEvento>(string tipo, TEvento evento) where TEvento : class;
}

/// <summary>Inbox para consumidores idempotentes: efectos exactamente una vez sobre entrega al menos una vez.</summary>
public interface IInbox
{
    Task<bool> YaProcesadoAsync(Guid messageId, CancellationToken ct);
    void Registrar(Guid messageId, string tipo);
}

/// <summary>Confirma en una sola transacción los cambios, el outbox y el inbox.</summary>
public interface IUnitOfWork
{
    /// <exception cref="Errores.ConflictoConcurrenciaException">Si otra transacción ganó la carrera.</exception>
    Task GuardarCambiosAsync(CancellationToken ct);
}
