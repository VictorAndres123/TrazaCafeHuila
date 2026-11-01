using Microsoft.EntityFrameworkCore;
using TrazaCafe.Acopio.Application.Errores;
using TrazaCafe.Acopio.Application.Puertos;
using TrazaCafe.Acopio.Domain;
using TrazaCafe.BuildingBlocks.Inbox;
using TrazaCafe.BuildingBlocks.Outbox;

namespace TrazaCafe.Acopio.Infrastructure.Persistencia;

internal sealed class EfFincaRepository(AcopioDbContext db) : IFincaRepository
{
    public async Task<IReadOnlyList<Finca>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Fincas.AsNoTracking().Where(f => ids.Contains(f.Id)).ToListAsync(ct);
}

internal sealed class EfEntregaRepository(AcopioDbContext db) : IEntregaRepository
{
    public async Task<IReadOnlyDictionary<Guid, string>> ObtenerHashesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Entregas.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Hash, ct);

    public async Task<IReadOnlyList<Entrega>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Entregas.Where(e => ids.Contains(e.Id)).ToListAsync(ct);

    public void Agregar(Entrega entrega) => db.Entregas.Add(entrega);
}

internal sealed class EfLoteRepository(AcopioDbContext db) : ILoteRepository
{
    public Task<Lote?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Lotes.FirstOrDefaultAsync(l => l.Id == id, ct);

    public void Agregar(Lote lote) => db.Lotes.Add(lote);
}

internal sealed class EfEventOutbox(AcopioDbContext db, TimeProvider reloj) : IEventOutbox
{
    public void Encolar<TEvento>(string tipo, TEvento evento) where TEvento : class =>
        db.Outbox.Add(OutboxMessage.Crear(tipo, evento, reloj.GetUtcNow()));
}

internal sealed class EfInbox(AcopioDbContext db, TimeProvider reloj) : IInbox
{
    public Task<bool> YaProcesadoAsync(Guid messageId, CancellationToken ct) =>
        db.Inbox.AsNoTracking().AnyAsync(m => m.MessageId == messageId, ct);

    public void Registrar(Guid messageId, string tipo) =>
        db.Inbox.Add(InboxMessage.Crear(messageId, tipo, reloj.GetUtcNow()));
}

/// <summary>Un único SaveChanges = una transacción con cambios de negocio + outbox + inbox.</summary>
internal sealed class EfUnitOfWork(AcopioDbContext db) : IUnitOfWork
{
    public async Task GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new ConflictoConcurrenciaException(
                "Otra operación modificó los mismos datos (por ejemplo, la entrega ya fue asignada a otro lote). Consulte y reintente.", ex);
        }
        catch (DbUpdateException ex) when (ex.EsViolacionDeUnicidad())
        {
            db.ChangeTracker.Clear();
            throw new ConflictoConcurrenciaException("Otra operación registró los mismos datos en paralelo.", ex);
        }
    }
}
