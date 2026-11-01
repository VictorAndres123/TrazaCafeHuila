using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrazaCafe.BuildingBlocks.Messaging;

namespace TrazaCafe.BuildingBlocks;

public static class Mensajeria
{
    /// <summary>Registra conexión, publicador, relay del outbox y consumidor de la cola del servicio.</summary>
    public static IServiceCollection AddMensajeriaConOutbox<TDbContext>(
        this IServiceCollection services, IConfiguration configuracion, DefinicionConsumo consumo)
        where TDbContext : DbContext
    {
        services.Configure<RabbitMqOptions>(configuracion.GetSection(RabbitMqOptions.Seccion));
        services.Configure<OutboxOptions>(configuracion.GetSection(OutboxOptions.Seccion));

        services.AddSingleton<RabbitMqConexion>();
        services.AddSingleton<RabbitMqPublicador>();
        services.AddSingleton<IPublicadorMensajes>(sp => sp.GetRequiredService<RabbitMqPublicador>());
        services.AddSingleton(consumo);

        services.AddHostedService<OutboxRelay<TDbContext>>();
        services.AddHostedService<RabbitMqConsumidor>();
        return services;
    }
}
