using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TrazaCafe.Acopio.Application.Errores;
using TrazaCafe.Acopio.Domain;

namespace TrazaCafe.Acopio.Api;

/// <summary>Traduce excepciones de dominio/aplicación a respuestas RFC 9457 (ProblemDetails).</summary>
internal sealed class ManejadorGlobalDeExcepciones(
    IProblemDetailsService problemDetails,
    ILogger<ManejadorGlobalDeExcepciones> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        var (estado, titulo) = excepcion switch
        {
            ValidacionException => (StatusCodes.Status400BadRequest, "Solicitud inválida"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Solicitud mal formada"),
            RecursoNoEncontradoException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
            ConflictoConcurrenciaException => (StatusCodes.Status409Conflict, "Conflicto de concurrencia"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio incumplida"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        if (estado >= 500)
            log.LogError(excepcion, "Error no controlado en {Ruta}", contexto.Request.Path);
        else
            log.LogInformation("{Titulo} en {Ruta}: {Mensaje}", titulo, contexto.Request.Path, excepcion.Message);

        ProblemDetails detalle = excepcion is ValidacionException validacion
            ? new ValidationProblemDetails(validacion.Errores)
            : new ProblemDetails { Detail = estado >= 500 ? "Ocurrió un error inesperado." : excepcion.Message };
        detalle.Status = estado;
        detalle.Title = titulo;

        contexto.Response.StatusCode = estado;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = detalle,
            Exception = excepcion
        });
    }
}
