namespace TrazaCafe.Cumplimiento.Domain;

/// <summary>
/// Regla de la fecha de corte del Reglamento (UE) 2023/1115: el producto no puede provenir de tierras
/// deforestadas después del 31 de diciembre de 2020.
/// </summary>
public static class ReglaEudr
{
    public static readonly DateOnly FechaDeCorte = new(2020, 12, 31);

    public static bool EsPosteriorAlCorte(DateOnly fechaPerdida) => fechaPerdida > FechaDeCorte;
}

/// <summary>Pérdida de cobertura forestal que intersecta una finca, según el proveedor geoespacial.</summary>
public sealed record PerdidaDeBosque(string AreaId, DateOnly FechaPerdida);
