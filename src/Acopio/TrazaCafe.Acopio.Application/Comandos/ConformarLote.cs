using TrazaCafe.Acopio.Application.Errores;
using TrazaCafe.Acopio.Application.Puertos;
using TrazaCafe.Acopio.Domain;
using TrazaCafe.Contracts;

namespace TrazaCafe.Acopio.Application.Comandos;

public sealed record ConformarLoteCommand(IReadOnlyList<Guid> EntregaIds);

public sealed record LoteConformadoResultado(Guid LoteId, string Codigo, string Estado, decimal KilosTotales);

/// <summary>
/// RF2 — Conformar un lote con balance de masa. La verificación ocurre después, de forma asíncrona (ADR-004).
/// </summary>
public sealed class ConformarLoteHandler(
    IEntregaRepository entregas,
    IFincaRepository fincas,
    ILoteRepository lotes,
    IEventOutbox outbox,
    IUnitOfWork unitOfWork,
    TimeProvider reloj)
{
    public async Task<LoteConformadoResultado> EjecutarAsync(ConformarLoteCommand comando, CancellationToken ct)
    {
        if (comando.EntregaIds is null || comando.EntregaIds.Count == 0)
            throw ValidacionException.De("entregaIds", "Debe indicar al menos una entrega.");

        var ids = comando.EntregaIds.Distinct().ToList();
        if (ids.Count != comando.EntregaIds.Count)
            throw ValidacionException.De("entregaIds", "Hay entregas repetidas en la solicitud.");

        var seleccionadas = await entregas.ObtenerPorIdsAsync(ids, ct);
        var faltantes = ids.Except(seleccionadas.Select(e => e.Id)).ToList();
        if (faltantes.Count > 0)
            throw new RecursoNoEncontradoException($"Entregas no encontradas: {string.Join(", ", faltantes)}.");

        var idsFincas = seleccionadas.Select(e => e.FincaId).Distinct().ToList();
        var conConsentimiento = (await fincas.ObtenerPorIdsAsync(idsFincas, ct))
            .Where(f => f.ConsentimientoDatos).Select(f => f.Id).ToHashSet();

        var lote = Lote.Conformar(Guid.CreateVersion7(), seleccionadas, conConsentimiento, reloj.GetUtcNow());

        lotes.Agregar(lote);
        outbox.Encolar(TiposDeEvento.LoteConformado, new LoteConformadoV1(
            lote.Id, lote.Codigo, lote.FincaIds.ToList(), lote.KilosTotales, lote.ConformadoEn));

        // Concurrencia optimista (xmin): si otro analista asignó alguna entrega, falla con 409.
        await unitOfWork.GuardarCambiosAsync(ct);

        return new LoteConformadoResultado(lote.Id, lote.Codigo, lote.Estado.ToString(), lote.KilosTotales);
    }
}
