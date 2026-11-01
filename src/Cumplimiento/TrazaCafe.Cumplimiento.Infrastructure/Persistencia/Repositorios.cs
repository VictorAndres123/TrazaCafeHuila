using Microsoft.EntityFrameworkCore;
using TrazaCafe.BuildingBlocks.Inbox;
using TrazaCafe.BuildingBlocks.Outbox;
using TrazaCafe.Cumplimiento.Application;
using TrazaCafe.Cumplimiento.Domain;

namespace TrazaCafe.Cumplimiento.Infrastructure.Persistencia;

internal sealed class EfFincaReplicaRepository(CumplimientoDbContext db) : IFincaReplicaRepository
{
    public Task<FincaReplica?> ObtenerAsync(Guid fincaId, CancellationToken ct) =>
        db.Fincas.FirstOrDefaultAsync(f => f.FincaId == fincaId, ct);

    public async Task<IReadOnlyList<FincaReplica>> ObtenerPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Fincas.AsNoTracking().Where(f => ids.Contains(f.FincaId)).ToListAsync(ct);

    public void Agregar(FincaReplica finca) => db.Fincas.Add(finca);
}

internal sealed class EfVerificacionRepository(CumplimientoDbContext db) : IVerificacionRepository
{
    public Task<bool> ExisteParaLoteAsync(Guid loteId, CancellationToken ct) =>
        db.Verificaciones.AsNoTracking().AnyAsync(v => v.LoteId == loteId, ct);

    public void Agregar(VerificacionLote verificacion) => db.Verificaciones.Add(verificacion);
}

internal sealed class EfEventOutbox(CumplimientoDbContext db, TimeProvider reloj) : IEventOutbox
{
    public void Encolar<TEvento>(string tipo, TEvento evento) where TEvento : class =>
        db.Outbox.Add(OutboxMessage.Crear(tipo, evento, reloj.GetUtcNow()));
}

internal sealed class EfInbox(CumplimientoDbContext db, TimeProvider reloj) : IInbox
{
    public Task<bool> YaProcesadoAsync(Guid messageId, CancellationToken ct) =>
        db.Inbox.AsNoTracking().AnyAsync(m => m.MessageId == messageId, ct);

    public void Registrar(Guid messageId, string tipo) =>
        db.Inbox.Add(InboxMessage.Crear(messageId, tipo, reloj.GetUtcNow()));
}

internal sealed class EfUnitOfWork(CumplimientoDbContext db) : IUnitOfWork
{
    public async Task GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.EsViolacionDeUnicidad())
        {
            // Entrega duplicada concurrente: el otro consumidor ya registró el inbox. El reintento lo detectará.
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
