using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TrazaCafe.Acopio.Application.Comandos;
using TrazaCafe.Acopio.Application.Consultas;
using TrazaCafe.Acopio.Application.Puertos;
using TrazaCafe.Acopio.Infrastructure.Lectura;
using TrazaCafe.Acopio.Infrastructure.Mensajeria;
using TrazaCafe.Acopio.Infrastructure.Persistencia;
using TrazaCafe.BuildingBlocks;
using TrazaCafe.BuildingBlocks.Health;
using TrazaCafe.BuildingBlocks.Messaging;

namespace TrazaCafe.Acopio.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Raíz de composición del servicio: conecta cada puerto con su adaptador.</summary>
    public static IServiceCollection AddAcopio(this IServiceCollection services, IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString("Acopio")
                     ?? throw new InvalidOperationException("Falta ConnectionStrings:Acopio.");

        // Casos de uso (aplicación)
        services.AddScoped<SincronizarEntregasHandler>();
        services.AddScoped<ConformarLoteHandler>();
        services.AddScoped<AplicarVerificacionHandler>();
        services.AddScoped<ConsultarGenealogiaHandler>();

        // Escritura: EF Core
        services.AddDbContext<AcopioDbContext>(o => o.UseNpgsql(cadena));
        services.AddScoped<IFincaRepository, EfFincaRepository>();
        services.AddScoped<IEntregaRepository, EfEntregaRepository>();
        services.AddScoped<ILoteRepository, EfLoteRepository>();
        services.AddScoped<IEventOutbox, EfEventOutbox>();
        services.AddScoped<IInbox, EfInbox>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Lectura: Npgsql directo
        services.AddSingleton(_ => NpgsqlDataSource.Create(cadena));
        services.AddSingleton<IGenealogiaReadModel, NpgsqlGenealogiaReadModel>();

        // Mensajería: outbox relay + consumidor de la cola acopio.eventos
        services.AddScoped<IManejadorMensaje, LoteVerificadoManejador>();
        services.AddMensajeriaConOutbox<AcopioDbContext>(configuracion, TopologiaTrazaCafe.Acopio);

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck<AcopioDbContext>>("postgres", tags: [EndpointsDeSalud.EtiquetaReady])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [EndpointsDeSalud.EtiquetaReady]);

        return services;
    }
}
