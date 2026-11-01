using TrazaCafe.Contracts;

namespace TrazaCafe.BuildingBlocks.Messaging;

/// <summary>
/// Topología del broker como código. Cualquier servicio que arranque la declara completa (operación idempotente),
/// de modo que ningún evento se publique hacia un exchange sin la cola suscriptora creada
/// (p. ej., los FincaRegistrada del seed si la API arranca antes que el worker).
/// </summary>
public static class TopologiaTrazaCafe
{
    public static readonly DefinicionConsumo Acopio =
        new("acopio.eventos", [TiposDeEvento.LoteVerificado]);

    public static readonly DefinicionConsumo Cumplimiento =
        new("cumplimiento.eventos", [TiposDeEvento.FincaRegistrada, TiposDeEvento.LoteConformado]);

    public static IReadOnlyList<DefinicionConsumo> Todas { get; } = [Acopio, Cumplimiento];
}
