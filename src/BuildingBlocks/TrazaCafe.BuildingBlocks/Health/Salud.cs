using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TrazaCafe.BuildingBlocks.Messaging;

namespace TrazaCafe.BuildingBlocks.Health;

public sealed class PostgresHealthCheck<TDbContext>(TDbContext db) : IHealthCheck where TDbContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("PostgreSQL accesible")
            : HealthCheckResult.Unhealthy("No hay conexión con PostgreSQL");
}

public sealed class RabbitMqHealthCheck(RabbitMqConexion conexion) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(conexion.EstaAbierta
            ? HealthCheckResult.Healthy("Conexión AMQP abierta")
            : HealthCheckResult.Unhealthy("Sin conexión AMQP"));
}

public static class EndpointsDeSalud
{
    public const string EtiquetaReady = "ready";

    /// <summary>/health/live: el proceso responde. /health/ready: dependencias (PostgreSQL, RabbitMQ...) disponibles.</summary>
    public static WebApplication MapEndpointsDeSalud(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = EscribirAsync
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(EtiquetaReady),
            ResponseWriter = EscribirAsync
        });
        return app;
    }

    private static Task EscribirAsync(HttpContext contexto, HealthReport reporte)
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";
        var cuerpo = new
        {
            estado = reporte.Status.ToString(),
            duracionMs = Math.Round(reporte.TotalDuration.TotalMilliseconds, 1),
            dependencias = reporte.Entries.Select(e => new
            {
                nombre = e.Key,
                estado = e.Value.Status.ToString(),
                descripcion = e.Value.Description
            })
        };
        return contexto.Response.WriteAsync(JsonSerializer.Serialize(cuerpo));
    }
}
