using System.Text.Json;
using Npgsql;
using TrazaCafe.Acopio.Application.Consultas;

namespace TrazaCafe.Acopio.Infrastructure.Lectura;

/// <summary>
/// Lado de lectura (CQRS): SQL directo sobre la vista vw_genealogia_lote, sin EF Core ni dominio.
/// </summary>
internal sealed class NpgsqlGenealogiaReadModel(NpgsqlDataSource fuente) : IGenealogiaReadModel
{
    public async Task<GenealogiaLoteDto?> ObtenerGenealogiaAsync(Guid loteId, CancellationToken ct)
    {
        await using var conexion = await fuente.OpenConnectionAsync(ct);

        Guid id; string codigo, estado; decimal kilos; DateTimeOffset conformado; DateTimeOffset? verificado;
        await using (var cmd = new NpgsqlCommand(
            "SELECT id, codigo, estado, kilos_totales, conformado_en, verificado_en FROM lotes WHERE id = $1", conexion))
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = loteId });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            id = r.GetGuid(0);
            codigo = r.GetString(1);
            estado = r.GetString(2);
            kilos = r.GetDecimal(3);
            conformado = r.GetFieldValue<DateTimeOffset>(4);
            verificado = r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5);
        }

        var filas = new List<FilaGenealogia>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT finca_id, finca_codigo, finca_nombre, municipio, vereda, geometria::text,
                   conforme, detalle_verificacion, entrega_id, fecha_entrega, punto_acopio, kilos
            FROM vw_genealogia_lote
            WHERE lote_id = $1
            ORDER BY finca_codigo, fecha_entrega, entrega_id
            """, conexion))
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = loteId });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                filas.Add(new FilaGenealogia(
                    r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5),
                    r.IsDBNull(6) ? null : r.GetBoolean(6),
                    r.IsDBNull(7) ? null : r.GetString(7),
                    r.GetGuid(8), r.GetFieldValue<DateOnly>(9), r.GetString(10), r.GetDecimal(11)));
            }
        }

        var fincas = filas
            .GroupBy(f => f.FincaId)
            .Select(g =>
            {
                var p = g.First();
                using var geometria = JsonDocument.Parse(p.GeometriaJson);
                return new FincaEnLoteDto(
                    p.FincaId, p.FincaCodigo, p.FincaNombre, p.Municipio, p.Vereda,
                    geometria.RootElement.Clone(),
                    g.Sum(x => x.Kilos),
                    p.Conforme, p.Detalle,
                    g.Select(x => new EntregaEnLoteDto(x.EntregaId, x.FechaEntrega, x.PuntoAcopio, x.Kilos)).ToList());
            })
            .ToList();

        double? segundos = verificado.HasValue ? Math.Round((verificado.Value - conformado).TotalSeconds, 3) : null;
        return new GenealogiaLoteDto(id, codigo, estado, kilos, conformado, verificado, segundos, fincas);
    }

    public async Task<IReadOnlyList<LoteResumenDto>> ListarLotesAsync(CancellationToken ct)
    {
        await using var cmd = fuente.CreateCommand("""
            SELECT l.id, l.codigo, l.estado, l.kilos_totales,
                   (SELECT count(*) FROM lote_entregas le WHERE le.lote_id = l.id)::int,
                   (SELECT count(DISTINCT le.finca_id) FROM lote_entregas le WHERE le.lote_id = l.id)::int,
                   l.conformado_en, l.verificado_en
            FROM lotes l
            ORDER BY l.conformado_en DESC
            LIMIT 100
            """);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lotes = new List<LoteResumenDto>();
        while (await r.ReadAsync(ct))
            lotes.Add(new LoteResumenDto(
                r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetInt32(4), r.GetInt32(5),
                r.GetFieldValue<DateTimeOffset>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7)));
        return lotes;
    }

    public async Task<IReadOnlyList<FincaResumenDto>> ListarFincasAsync(CancellationToken ct)
    {
        await using var cmd = fuente.CreateCommand(
            "SELECT id, codigo, nombre, municipio, vereda, consentimiento_datos FROM fincas ORDER BY codigo");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var fincas = new List<FincaResumenDto>();
        while (await r.ReadAsync(ct))
            fincas.Add(new FincaResumenDto(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetBoolean(5)));
        return fincas;
    }

    private sealed record FilaGenealogia(
        Guid FincaId, string FincaCodigo, string FincaNombre, string Municipio, string Vereda, string GeometriaJson,
        bool? Conforme, string? Detalle, Guid EntregaId, DateOnly FechaEntrega, string PuntoAcopio, decimal Kilos);
}
