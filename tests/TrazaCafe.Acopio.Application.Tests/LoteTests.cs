using TrazaCafe.Acopio.Application.Comandos;
using TrazaCafe.Acopio.Domain;
using TrazaCafe.Contracts;

namespace TrazaCafe.Acopio.Application.Tests;

public sealed class ConformarYVerificarLoteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 31, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid FincaA = Guid.Parse("f1000000-0000-4000-8000-000000000001");
    private static readonly Guid FincaB = Guid.Parse("f1000000-0000-4000-8000-000000000002");

    private readonly BaseEnMemoria _db = new();
    private readonly RelojFijo _reloj = new(Ahora);

    public ConformarYVerificarLoteTests()
    {
        _db.Fincas.Add(Finca.Registrar(FincaA, "F-001", "La Esperanza", "Pitalito", "Bruselas", "{}", true, Ahora));
        _db.Fincas.Add(Finca.Registrar(FincaB, "F-002", "El Mirador", "Acevedo", "San Adolfo", "{}", true, Ahora));
        _db.Entregas.Add(Entrega.Registrar(Guid.NewGuid(), FincaA, "Acopio Pitalito", 100m, new DateOnly(2026, 10, 20), Ahora));
        _db.Entregas.Add(Entrega.Registrar(Guid.NewGuid(), FincaB, "Acopio Pitalito", 50m, new DateOnly(2026, 10, 21), Ahora));
    }

    private async Task<LoteConformadoResultado> ConformarAsync()
    {
        var handler = new ConformarLoteHandler(new EntregasFake(_db), new FincasFake(_db), new LotesFake(_db),
            new OutboxFake(_db), _db, _reloj);
        return await handler.EjecutarAsync(new ConformarLoteCommand(_db.Entregas.Select(e => e.Id).ToList()), CancellationToken.None);
    }

    [Fact(DisplayName = "Conformar publica LoteConformado en el outbox, en la misma transacción que el lote")]
    public async Task Conformar_encola_evento_en_la_misma_transaccion()
    {
        var resultado = await ConformarAsync();

        Assert.Equal("PendienteVerificacion", resultado.Estado);
        Assert.Equal(150m, resultado.KilosTotales);
        Assert.Single(_db.Lotes);
        var (tipo, evento) = Assert.Single(_db.Outbox);
        Assert.Equal(TiposDeEvento.LoteConformado, tipo);
        var conformado = Assert.IsType<LoteConformadoV1>(evento);
        Assert.Equal(2, conformado.FincaIds.Count);
        Assert.Equal(1, _db.Confirmaciones);
    }

    [Fact(DisplayName = "Inbox · Un LoteVerificado entregado dos veces se aplica una sola vez")]
    public async Task LoteVerificado_duplicado_se_aplica_una_vez()
    {
        var lote = await ConformarAsync();
        var handler = new AplicarVerificacionHandler(new LotesFake(_db), new InboxFake(_db), _db, _reloj);
        var messageId = Guid.NewGuid();
        var evento = new LoteVerificadoV1(lote.LoteId,
            [new ResultadoFincaV1(FincaA, true, "ok"), new ResultadoFincaV1(FincaB, false, "Pérdida 2023")], Ahora);

        var primera = await handler.EjecutarAsync(messageId, evento, CancellationToken.None);
        var segunda = await handler.EjecutarAsync(messageId, evento, CancellationToken.None); // redelivery

        Assert.True(primera);
        Assert.False(segunda);
        Assert.Equal(EstadoLote.Bloqueado, _db.Lotes.Single().Estado);
        Assert.Equal(2, _db.Lotes.Single().Verificaciones.Count);
    }
}
