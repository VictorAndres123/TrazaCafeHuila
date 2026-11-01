using Microsoft.EntityFrameworkCore;
using TrazaCafe.Acopio.Domain;
using TrazaCafe.BuildingBlocks.Inbox;
using TrazaCafe.BuildingBlocks.Outbox;

namespace TrazaCafe.Acopio.Infrastructure.Persistencia;

/// <summary>
/// Modelo de escritura. El esquema lo crean los scripts versionados de db/init (no hay migraciones de EF en el MVP).
/// </summary>
public sealed class AcopioDbContext(DbContextOptions<AcopioDbContext> options) : DbContext(options)
{
    public DbSet<Finca> Fincas => Set<Finca>();
    public DbSet<Entrega> Entregas => Set<Entrega>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Finca>(b =>
        {
            b.ToTable("fincas");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(x => x.Codigo).HasColumnName("codigo");
            b.Property(x => x.Nombre).HasColumnName("nombre");
            b.Property(x => x.Municipio).HasColumnName("municipio");
            b.Property(x => x.Vereda).HasColumnName("vereda");
            b.Property(x => x.GeometriaGeoJson).HasColumnName("geometria").HasColumnType("jsonb");
            b.Property(x => x.ConsentimientoDatos).HasColumnName("consentimiento_datos");
            b.Property(x => x.RegistradaEn).HasColumnName("registrada_en");
        });

        modelBuilder.Entity<Entrega>(b =>
        {
            b.ToTable("entregas");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(x => x.FincaId).HasColumnName("finca_id");
            b.Property(x => x.PuntoAcopio).HasColumnName("punto_acopio");
            b.Property(x => x.Kilos).HasColumnName("kilos").HasPrecision(12, 3);
            b.Property(x => x.FechaEntrega).HasColumnName("fecha_entrega");
            b.Property(x => x.Hash).HasColumnName("hash");
            b.Property(x => x.LoteId).HasColumnName("lote_id");
            b.Property(x => x.RegistradaEn).HasColumnName("registrada_en");
            b.Ignore(x => x.EstaAsignada);
            ConfigurarXmin(b);
        });

        modelBuilder.Entity<Lote>(b =>
        {
            b.ToTable("lotes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(x => x.Codigo).HasColumnName("codigo");
            b.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>();
            b.Property(x => x.KilosTotales).HasColumnName("kilos_totales").HasPrecision(14, 3);
            b.Property(x => x.ConformadoEn).HasColumnName("conformado_en");
            b.Property(x => x.VerificadoEn).HasColumnName("verificado_en");
            b.Ignore(x => x.FincaIds);
            b.Ignore(x => x.EstaFinalizado);
            ConfigurarXmin(b);

            b.OwnsMany(x => x.Entregas, e =>
            {
                e.ToTable("lote_entregas");
                e.WithOwner().HasForeignKey("LoteId");
                e.Property<Guid>("LoteId").HasColumnName("lote_id");
                e.Property(x => x.EntregaId).HasColumnName("entrega_id").ValueGeneratedNever();
                e.Property(x => x.FincaId).HasColumnName("finca_id");
                e.Property(x => x.Kilos).HasColumnName("kilos").HasPrecision(12, 3);
                e.HasKey("LoteId", nameof(LoteEntrega.EntregaId));
            });

            b.OwnsMany(x => x.Verificaciones, v =>
            {
                v.ToTable("lote_verificaciones");
                v.WithOwner().HasForeignKey("LoteId");
                v.Property<Guid>("LoteId").HasColumnName("lote_id");
                v.Property(x => x.FincaId).HasColumnName("finca_id").ValueGeneratedNever();
                v.Property(x => x.Conforme).HasColumnName("conforme");
                v.Property(x => x.Detalle).HasColumnName("detalle");
                v.HasKey("LoteId", nameof(VerificacionFinca.FincaId));
            });

            b.Navigation(x => x.Entregas).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Navigation(x => x.Verificaciones).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.MapearOutboxEInbox();
    }

    /// <summary>
    /// Concurrencia optimista con la columna de sistema xmin de PostgreSQL (ADR-005):
    /// el UPDATE incluye "WHERE xmin = @original"; si otra transacción cambió la fila, afecta 0 filas.
    /// </summary>
    private static void ConfigurarXmin<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> b) where T : class =>
        b.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
}
