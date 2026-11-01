using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using TrazaCafe.BuildingBlocks.Outbox;

namespace TrazaCafe.BuildingBlocks.Messaging;

/// <summary>
/// Relevo del outbox: lee eventos pendientes con <c>FOR UPDATE SKIP LOCKED</c> (seguro con varias réplicas),
/// los publica con confirmación del broker y los marca como publicados.
/// Garantía: entrega AL MENOS UNA VEZ (si falla tras publicar y antes de marcar, se republica; el inbox lo absorbe).
/// </summary>
public sealed class OutboxRelay<TDbContext>(
    IServiceScopeFactory scopes,
    IPublicadorMensajes publicador,
    IOptions<OutboxOptions> opciones,
    TimeProvider reloj,
    ILogger<OutboxRelay<TDbContext>> log) : BackgroundService
    where TDbContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Outbox relay iniciado para {DbContext}", typeof(TDbContext).Name);
        using var temporizador = new PeriodicTimer(TimeSpan.FromMilliseconds(opciones.Value.IntervaloMs));

        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    while (await PublicarPendientesAsync(stoppingToken) > 0) { /* drenar mientras haya pendientes */ }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Error en el ciclo del outbox relay; se reintentará");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado normal
        }
    }

    private async Task<int> PublicarPendientesAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var pendientes = await db.Set<OutboxMessage>()
            .FromSqlRaw("""
                SELECT * FROM outbox_messages
                WHERE publicado_en IS NULL
                ORDER BY ocurrido_en, id
                LIMIT {0}
                FOR UPDATE SKIP LOCKED
                """, opciones.Value.TamanoLote)
            .ToListAsync(ct);

        if (pendientes.Count == 0)
        {
            await transaccion.CommitAsync(ct);
            return 0;
        }

        var publicados = 0;
        foreach (var mensaje in pendientes)
        {
            try
            {
                await publicador.PublicarEventoAsync(
                    new MensajeSaliente(mensaje.Id, mensaje.Tipo, Encoding.UTF8.GetBytes(mensaje.Payload), mensaje.OcurridoEn), ct);
                mensaje.MarcarPublicado(reloj.GetUtcNow());
                publicados++;
                log.LogInformation("Outbox → RabbitMQ: {Tipo} {MessageId}", mensaje.Tipo, mensaje.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mensaje.RegistrarFallo(ex.Message);
                log.LogWarning("No se pudo publicar {MessageId}: {Error}", mensaje.Id, ex.Message);
                break; // conservar el orden: se reintenta en el próximo ciclo
            }
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return publicados;
    }
}
