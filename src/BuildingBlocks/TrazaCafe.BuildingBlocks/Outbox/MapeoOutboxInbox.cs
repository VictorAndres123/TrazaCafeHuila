using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrazaCafe.BuildingBlocks.Inbox;

namespace TrazaCafe.BuildingBlocks.Outbox;

public static class MapeoOutboxInbox
{
    /// <summary>Mapea las tablas outbox_messages e inbox_messages (creadas por los scripts de db/init).</summary>
    public static ModelBuilder MapearOutboxEInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(x => x.Tipo).HasColumnName("tipo");
            b.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            b.Property(x => x.OcurridoEn).HasColumnName("ocurrido_en");
            b.Property(x => x.PublicadoEn).HasColumnName("publicado_en");
            b.Property(x => x.Intentos).HasColumnName("intentos");
            b.Property(x => x.UltimoError).HasColumnName("ultimo_error");
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox_messages");
            b.HasKey(x => x.MessageId);
            b.Property(x => x.MessageId).HasColumnName("message_id").ValueGeneratedNever();
            b.Property(x => x.Tipo).HasColumnName("tipo");
            b.Property(x => x.ProcesadoEn).HasColumnName("procesado_en");
        });

        return modelBuilder;
    }

    /// <summary>¿La excepción proviene de una violación de clave única en PostgreSQL?</summary>
    public static bool EsViolacionDeUnicidad(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
