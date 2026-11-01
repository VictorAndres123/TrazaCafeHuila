namespace TrazaCafe.Acopio.Application.Errores;

/// <summary>Datos de entrada inválidos (400).</summary>
public sealed class ValidacionException(IDictionary<string, string[]> errores)
    : Exception("La solicitud no es válida.")
{
    public IDictionary<string, string[]> Errores { get; } = errores;

    public static ValidacionException De(string campo, string mensaje) =>
        new(new Dictionary<string, string[]> { [campo] = [mensaje] });
}

/// <summary>El recurso solicitado no existe (404).</summary>
public sealed class RecursoNoEncontradoException(string mensaje) : Exception(mensaje);

/// <summary>
/// Otra transacción modificó los mismos datos (concurrencia optimista o restricción única) (409).
/// </summary>
public sealed class ConflictoConcurrenciaException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);
