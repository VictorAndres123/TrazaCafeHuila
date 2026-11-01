using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TrazaCafe.Cumplimiento.Infrastructure.Geoespacial;

internal sealed class GeodataHealthCheck(NtsVerificadorDeforestacion verificador) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(verificador.AreasCargadas > 0
            ? HealthCheckResult.Healthy($"{verificador.AreasCargadas} áreas con pérdida de bosque indexadas")
            : HealthCheckResult.Degraded("La capa de áreas deforestadas está vacía"));
}
