using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace TrazaCafe.BuildingBlocks.Messaging;

/// <summary>Conexión AMQP compartida (una por proceso), creada de forma perezosa y con reintentos al arrancar.</summary>
public sealed class RabbitMqConexion(IOptions<RabbitMqOptions> opciones, ILogger<RabbitMqConexion> log) : IAsyncDisposable
{
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;

    public bool EstaAbierta => _conexion?.IsOpen == true;

    public async Task<IConnection> ObtenerAsync(CancellationToken ct)
    {
        if (_conexion is { IsOpen: true }) return _conexion;

        await _candado.WaitAsync(ct);
        try
        {
            if (_conexion is { IsOpen: true }) return _conexion;

            var o = opciones.Value;
            var fabrica = new ConnectionFactory
            {
                HostName = o.Host,
                Port = o.Port,
                UserName = o.Usuario,
                Password = o.Clave,
                VirtualHost = o.VirtualHost,
                ClientProvidedName = o.NombreCliente,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true
            };

            for (var intento = 1; ; intento++)
            {
                try
                {
                    _conexion = await fabrica.CreateConnectionAsync(ct);
                    log.LogInformation("Conectado a RabbitMQ {Host}:{Port}", o.Host, o.Port);
                    return _conexion;
                }
                catch (Exception ex) when (ex is not OperationCanceledException && intento < 30)
                {
                    log.LogWarning("RabbitMQ no disponible (intento {Intento}/30): {Error}", intento, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
            }
        }
        finally
        {
            _candado.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
        {
            try { await _conexion.CloseAsync(); } catch { /* cierre best-effort */ }
            await _conexion.DisposeAsync();
        }
        _candado.Dispose();
    }
}
