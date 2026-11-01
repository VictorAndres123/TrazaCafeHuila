using TrazaCafe.Acopio.Application.Puertos;
using TrazaCafe.Acopio.Domain;

namespace TrazaCafe.Acopio.Application.Tests;

/// <summary>
/// "Base de datos" en memoria con semántica transaccional mínima: lo agregado queda pendiente
/// y solo se vuelve visible al confirmar la unidad de trabajo.
/// </summary>
internal sealed class BaseEnMemoria : IUnitOfWork
{
    public List<Finca> Fincas { get; } = [];
    public List<Entrega> Entregas { get; } = [];
    public List<Lote> Lotes { get; } = [];
    public List<(string Tipo, object Evento)> Outbox { get; } = [];
    public HashSet<Guid> Inbox { get; } = [];

    internal readonly List<Action> Pendientes = [];
    public int Confirmaciones { get; private set; }

    public Task GuardarCambiosAsync(CancellationToken ct)
    {
        foreach (var aplicar in Pendientes) aplicar();
        Pendientes.Clear();
        Confirmaciones++;
        return Task.CompletedTask;
    }
}

internal sealed class FincasFake(BaseEnMemoria db) : IFincaRepository
{
    public Task<IReadOnlyList<Finca>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Finca>>(db.Fincas.Where(f => ids.Contains(f.Id)).ToList());
}

internal sealed class EntregasFake(BaseEnMemoria db) : IEntregaRepository
{
    public Task<IReadOnlyDictionary<Guid, string>> ObtenerHashesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            db.Entregas.Where(e => ids.Contains(e.Id)).ToDictionary(e => e.Id, e => e.Hash));

    public Task<IReadOnlyList<Entrega>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Entrega>>(db.Entregas.Where(e => ids.Contains(e.Id)).ToList());

    public void Agregar(Entrega entrega) => db.Pendientes.Add(() => db.Entregas.Add(entrega));
}

internal sealed class LotesFake(BaseEnMemoria db) : ILoteRepository
{
    public Task<Lote?> ObtenerAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(db.Lotes.FirstOrDefault(l => l.Id == id));

    public void Agregar(Lote lote) => db.Pendientes.Add(() => db.Lotes.Add(lote));
}

internal sealed class OutboxFake(BaseEnMemoria db) : IEventOutbox
{
    public void Encolar<TEvento>(string tipo, TEvento evento) where TEvento : class =>
        db.Pendientes.Add(() => db.Outbox.Add((tipo, evento)));
}

internal sealed class InboxFake(BaseEnMemoria db) : IInbox
{
    public Task<bool> YaProcesadoAsync(Guid messageId, CancellationToken ct) => Task.FromResult(db.Inbox.Contains(messageId));

    public void Registrar(Guid messageId, string tipo) => db.Pendientes.Add(() => db.Inbox.Add(messageId));
}

internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}
