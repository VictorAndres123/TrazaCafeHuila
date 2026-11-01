using Microsoft.EntityFrameworkCore;
using TrazaCafe.BuildingBlocks.Inbox;
using TrazaCafe.BuildingBlocks.Outbox;
using TrazaCafe.Cumplimiento.Domain;

namespace TrazaCafe.Cumplimiento.Infrastructure.Persistencia;

public sealed class CumplimientoDbContext(DbContextOptions<CumplimientoDbContext> options) : DbContext(options)
{
    public DbSet<FincaReplica> Fincas => Set<FincaReplica>();
    public DbSet<VerificacionLote> Verificaciones => Set<VerificacionLote>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FincaReplica>(b =>
        {
            b.ToTable("fincas_replica");
            b.HasKey(x => x.FincaId);
            b.Property(x => x.FincaId).HasColumnName("finca_id").ValueGeneratedNever();
            b.Property(x => x.Codigo).HasColumnName("codigo");
            b.Property(x => x.GeometriaGeoJson).HasColumnName("geometria").HasColumnType("jsonb");
            b.Property(x => x.RecibidaEn).HasColumnName("recibida_en");
        });

        modelBuilder.Entity<VerificacionLote>(b =>
        {
            b.ToTable("verificaciones");
            b.HasKey(x => x.LoteId);
            b.Property(x => x.LoteId).HasColumnName("lote_id").ValueGeneratedNever();
            b.Property(x => x.Bloqueado).HasColumnName("bloqueado");
            b.Property(x => x.VerificadoEn).HasColumnName("verificado_en");

            b.OwnsMany(x => x.Resultados, r =>
            {
                r.ToTable("verificacion_fincas");
                r.WithOwner().HasForeignKey("LoteId");
                r.Property<Guid>("LoteId").HasColumnName("lote_id");
                r.Property(x => x.FincaId).HasColumnName("finca_id").ValueGeneratedNever();
                r.Property(x => x.Conforme).HasColumnName("conforme");
                r.Property(x => x.Detalle).HasColumnName("detalle");
                r.HasKey("LoteId", nameof(ResultadoFinca.FincaId));
            });
            b.Navigation(x => x.Resultados).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.MapearOutboxEInbox();
    }
}
