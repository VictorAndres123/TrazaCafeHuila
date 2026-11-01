namespace TrazaCafe.Acopio.Domain;

/// <summary>Ciclo de vida del lote con consistencia eventual explícita (ADR-004).</summary>
public enum EstadoLote
{
    PendienteVerificacion,
    Declarable,
    Bloqueado
}
