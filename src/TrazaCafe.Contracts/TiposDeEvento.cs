using System.Text.Json;

namespace TrazaCafe.Contracts;

/// <summary>
/// Nombres versionados de los eventos de integración. Se usan como routing key en RabbitMQ
/// y como propiedad <c>type</c> del mensaje. Cambiar un contrato implica crear una versión nueva (v2).
/// </summary>
public static class TiposDeEvento
{
    public const string FincaRegistrada = "acopio.finca-registrada.v1";
    public const string EntregaRegistrada = "acopio.entrega-registrada.v1";
    public const string LoteConformado = "acopio.lote-conformado.v1";
    public const string LoteVerificado = "cumplimiento.lote-verificado.v1";
}

/// <summary>Opciones de serialización compartidas para los contratos (camelCase).</summary>
public static class JsonContratos
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);
}
