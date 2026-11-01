using TrazaCafe.Acopio.Application.Comandos;
using TrazaCafe.Acopio.Application.Errores;
using TrazaCafe.Acopio.Domain;
using TrazaCafe.Contracts;

namespace TrazaCafe.Acopio.Application.Tests;

/// <summary>QA1 · Integridad: reenviar no duplica kilos; mismo id con contenido distinto → conflicto sin efectos.</summary>
public sealed class SincronizarEntregasTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 31, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid FincaA = Guid.Parse("f1000000-0000-4000-8000-000000000001");
    private static readonly Guid FincaB = Guid.Parse("f1000000-0000-4000-8000-000000000004");

    private readonly BaseEnMemoria _db = new();
    private readonly SincronizarEntregasHandler _handler;

    public SincronizarEntregasTests()
    {
        _db.Fincas.Add(Finca.Registrar(FincaA, "F-001", "La Esperanza", "Pitalito", "Bruselas", "{}", true, Ahora));
        _db.Fincas.Add(Finca.Registrar(FincaB, "F-004", "Los Naranjos", "Garzón", "Zuluaga", "{}", true, Ahora));
        _handler = new SincronizarEntregasHandler(
            new EntregasFake(_db), new FincasFake(_db), new OutboxFake(_db), _db, new RelojFijo(Ahora));
    }

    private static SincronizarEntregasCommand Comando(params EntregaPorSincronizar[] entregas) =>
        new("Acopio Pitalito", entregas);

    private static EntregaPorSincronizar E(Guid id, Guid finca, decimal kilos) =>
        new(id, finca, kilos, new DateOnly(2026, 10, 20));

    [Fact(DisplayName = "QA1 · Reenviar el mismo lote de sincronización no duplica kilos (200 sin efectos)")]
    public async Task Reenvio_identico_es_idempotente()
    {
        var comando = Comando(E(Guid.NewGuid(), FincaA, 125.5m), E(Guid.NewGuid(), FincaB, 80m));

        var primera = await _handler.EjecutarAsync(comando, CancellationToken.None);
        var segunda = await _handler.EjecutarAsync(comando, CancellationToken.None);

        var a1 = Assert.IsType<ResultadoSincronizacion.Aceptada>(primera);
        Assert.Equal(2, a1.Creadas.Count);
        Assert.Empty(a1.Duplicadas);

        var a2 = Assert.IsType<ResultadoSincronizacion.Aceptada>(segunda);
        Assert.Empty(a2.Creadas);
        Assert.Equal(2, a2.Duplicadas.Count);

        Assert.Equal(2, _db.Entregas.Count);
        Assert.Equal(205.5m, _db.Entregas.Sum(e => e.Kilos));                         // 0 kilos duplicados
        Assert.Equal(2, _db.Outbox.Count(o => o.Tipo == TiposDeEvento.EntregaRegistrada)); // 0 eventos duplicados
        Assert.Equal(1, _db.Confirmaciones);                                          // el reenvío no abrió transacción
    }

    [Fact(DisplayName = "QA1 · Reenvío parcial: las nuevas se crean y las ya sincronizadas se reportan como duplicadas")]
    public async Task Reenvio_parcial_crea_solo_las_nuevas()
    {
        var existente = E(Guid.NewGuid(), FincaA, 10m);
        await _handler.EjecutarAsync(Comando(existente), CancellationToken.None);

        var nueva = E(Guid.NewGuid(), FincaB, 20m);
        var resultado = await _handler.EjecutarAsync(Comando(existente, nueva), CancellationToken.None);

        var aceptada = Assert.IsType<ResultadoSincronizacion.Aceptada>(resultado);
        Assert.Equal(nueva.EntregaId, Assert.Single(aceptada.Creadas));
        Assert.Equal(existente.EntregaId, Assert.Single(aceptada.Duplicadas));
        Assert.Equal(2, _db.Entregas.Count);
    }

    [Fact(DisplayName = "QA1 · Mismo entregaId con contenido distinto → Conflicto (409) y no se persiste nada")]
    public async Task Mismo_id_con_contenido_distinto_es_conflicto_atomico()
    {
        var id = Guid.NewGuid();
        await _handler.EjecutarAsync(Comando(E(id, FincaA, 125.5m)), CancellationToken.None);

        var otraNueva = E(Guid.NewGuid(), FincaB, 30m);
        var alterada = E(id, FincaA, 1255m); // mismos ids, kilos alterados

        var resultado = await _handler.EjecutarAsync(Comando(otraNueva, alterada), CancellationToken.None);

        var conflicto = Assert.IsType<ResultadoSincronizacion.Conflicto>(resultado);
        Assert.Equal(id, Assert.Single(conflicto.EnConflicto));
        Assert.Single(_db.Entregas);                                  // la "otraNueva" tampoco se guardó
        Assert.Equal(125.5m, _db.Entregas.Single().Kilos);            // el original no cambió
        Assert.Empty(_db.Pendientes);
    }

    [Fact(DisplayName = "Una finca no registrada invalida toda la sincronización")]
    public async Task Finca_desconocida_es_invalida()
    {
        var comando = Comando(E(Guid.NewGuid(), FincaA, 10m), E(Guid.NewGuid(), Guid.NewGuid(), 10m));

        await Assert.ThrowsAsync<ValidacionException>(() => _handler.EjecutarAsync(comando, CancellationToken.None));
        Assert.Empty(_db.Entregas);
    }

    [Fact(DisplayName = "Identificadores repetidos en la misma solicitud son inválidos")]
    public async Task Ids_repetidos_en_la_solicitud_son_invalidos()
    {
        var id = Guid.NewGuid();
        var comando = Comando(E(id, FincaA, 10m), E(id, FincaA, 10m));

        await Assert.ThrowsAsync<ValidacionException>(() => _handler.EjecutarAsync(comando, CancellationToken.None));
    }

    [Fact(DisplayName = "Una entrega inválida (kilos ≤ 0) rechaza toda la solicitud")]
    public async Task Entrega_invalida_rechaza_todo()
    {
        var comando = Comando(E(Guid.NewGuid(), FincaA, 10m), E(Guid.NewGuid(), FincaA, 0m));

        await Assert.ThrowsAsync<DomainException>(() => _handler.EjecutarAsync(comando, CancellationToken.None));
        Assert.Empty(_db.Entregas);
    }
}
