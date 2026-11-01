using TrazaCafe.Acopio.Application.Errores;
using TrazaCafe.Acopio.Application.Puertos;
using TrazaCafe.Acopio.Domain;
using TrazaCafe.Contracts;

namespace TrazaCafe.Acopio.Application.Comandos;

public sealed record EntregaPorSincronizar(Guid EntregaId, Guid FincaId, decimal Kilos, DateOnly FechaEntrega);

public sealed record SincronizarEntregasCommand(string PuntoAcopio, IReadOnlyList<EntregaPorSincronizar> Entregas);

public abstract record ResultadoSincronizacion
{
    /// <summary>Se aceptó el lote de sincronización. Las duplicadas idénticas no tienen efecto.</summary>
    public sealed record Aceptada(IReadOnlyList<Guid> Creadas, IReadOnlyList<Guid> Duplicadas) : ResultadoSincronizacion;

    /// <summary>Mismo identificador con contenido distinto: posible corrupción o manipulación. No se persiste nada.</summary>
    public sealed record Conflicto(IReadOnlyList<Guid> EnConflicto) : ResultadoSincronizacion;
}

/// <summary>
/// RF1 — Sincronización idempotente de entregas registradas sin conexión (QA1, ADR-005).
/// </summary>
public sealed class SincronizarEntregasHandler(
    IEntregaRepository entregas,
    IFincaRepository fincas,
    IEventOutbox outbox,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public const int MaxEntregasPorSincronizacion = 500;

    public async Task<ResultadoSincronizacion> EjecutarAsync(SincronizarEntregasCommand comando, CancellationToken ct)
    {
        Validar(comando);
        var ahora = reloj.GetUtcNow();

        // 1. Construir candidatas: el dominio valida cada entrega (kilos, fechas...). Si una falla, nada se guarda.
        var candidatas = comando.Entregas
            .Select(e => Entrega.Registrar(e.EntregaId, e.FincaId, comando.PuntoAcopio, e.Kilos, e.FechaEntrega, ahora))
            .ToList();

        // 2. Las fincas deben existir.
        var idsFincas = candidatas.Select(e => e.FincaId).Distinct().ToList();
        var existentes = (await fincas.ObtenerPorIdsAsync(idsFincas, ct)).Select(f => f.Id).ToHashSet();
        var desconocidas = idsFincas.Where(id => !existentes.Contains(id)).ToList();
        if (desconocidas.Count > 0)
            throw ValidacionException.De("entregas[].fincaId",
                $"Fincas no registradas: {string.Join(", ", desconocidas)}.");

        // 3. Idempotencia: comparar contra lo ya registrado.
        var hashesPrevios = await entregas.ObtenerHashesAsync(candidatas.Select(e => e.Id).ToList(), ct);

        var enConflicto = candidatas
            .Where(e => hashesPrevios.TryGetValue(e.Id, out var previo) && previo != e.Hash)
            .Select(e => e.Id).ToList();
        if (enConflicto.Count > 0)
            return new ResultadoSincronizacion.Conflicto(enConflicto);

        var duplicadas = candidatas.Where(e => hashesPrevios.ContainsKey(e.Id)).Select(e => e.Id).ToList();
        var nuevas = candidatas.Where(e => !hashesPrevios.ContainsKey(e.Id)).ToList();

        if (nuevas.Count == 0)
            return new ResultadoSincronizacion.Aceptada([], duplicadas);

        // 4. Persistir entregas + eventos en una sola transacción (outbox).
        foreach (var entrega in nuevas)
        {
            entregas.Agregar(entrega);
            outbox.Encolar(TiposDeEvento.EntregaRegistrada, new EntregaRegistradaV1(
                entrega.Id, entrega.FincaId, entrega.PuntoAcopio, entrega.Kilos, entrega.FechaEntrega));
        }

        await unitOfWork.GuardarCambiosAsync(ct);
        return new ResultadoSincronizacion.Aceptada(nuevas.Select(e => e.Id).ToList(), duplicadas);
    }

    private static void Validar(SincronizarEntregasCommand comando)
    {
        var errores = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(comando.PuntoAcopio))
            errores["puntoAcopio"] = ["El punto de acopio es obligatorio."];

        if (comando.Entregas is null || comando.Entregas.Count == 0)
            errores["entregas"] = ["Debe enviar al menos una entrega."];
        else
        {
            if (comando.Entregas.Count > MaxEntregasPorSincronizacion)
                errores["entregas"] = [$"Máximo {MaxEntregasPorSincronizacion} entregas por sincronización."];

            var repetidas = comando.Entregas.GroupBy(e => e.EntregaId).Where(g => g.Count() > 1)
                .Select(g => g.Key.ToString()).ToList();
            if (repetidas.Count > 0)
                errores["entregas[].entregaId"] = [$"Identificadores repetidos en la misma solicitud: {string.Join(", ", repetidas)}."];
        }

        if (errores.Count > 0) throw new ValidacionException(errores);
    }
}
