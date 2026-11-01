using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrazaCafe.BuildingBlocks;
using TrazaCafe.BuildingBlocks.Health;
using TrazaCafe.BuildingBlocks.Messaging;
using TrazaCafe.Cumplimiento.Application;
using TrazaCafe.Cumplimiento.Infrastructure.Geoespacial;
using TrazaCafe.Cumplimiento.Infrastructure.Mensajeria;
using TrazaCafe.Cumplimiento.Infrastructure.Persistencia;

namespace TrazaCafe.Cumplimiento.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCumplimiento(this IServiceCollection services, IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString("Cumplimiento")
                     ?? throw new InvalidOperationException("Falta ConnectionStrings:Cumplimiento.");

        // Casos de uso
        services.AddScoped<RegistrarFincaReplicaHandler>();
        services.AddScoped<VerificarLoteHandler>();

        // Persistencia
        services.AddDbContext<CumplimientoDbContext>(o => o.UseNpgsql(cadena));
        services.AddScoped<IFincaReplicaRepository, EfFincaReplicaRepository>();
        services.AddScoped<IVerificacionRepository, EfVerificacionRepository>();
        services.AddScoped<IEventOutbox, EfEventOutbox>();
        services.AddScoped<IInbox, EfInbox>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Adaptador geoespacial (ADR-006): cambiar esta línea es todo lo que exige pasar a PostGIS.
        services.Configure<GeodataOptions>(configuracion.GetSection(GeodataOptions.Seccion));
        services.AddSingleton<NtsVerificadorDeforestacion>();
        services.AddSingleton<IVerificadorDeforestacion>(sp => sp.GetRequiredService<NtsVerificadorDeforestacion>());

        // Mensajería
        services.AddScoped<IManejadorMensaje, FincaRegistradaManejador>();
        services.AddScoped<IManejadorMensaje, LoteConformadoManejador>();
        services.AddMensajeriaConOutbox<CumplimientoDbContext>(configuracion, TopologiaTrazaCafe.Cumplimiento);

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck<CumplimientoDbContext>>("postgres", tags: [EndpointsDeSalud.EtiquetaReady])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [EndpointsDeSalud.EtiquetaReady])
            .AddCheck<GeodataHealthCheck>("geodata", tags: [EndpointsDeSalud.EtiquetaReady]);

        return services;
    }
}
