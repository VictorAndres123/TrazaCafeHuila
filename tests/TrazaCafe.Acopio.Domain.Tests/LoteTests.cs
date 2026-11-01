using TrazaCafe.Acopio.Domain;
using static TrazaCafe.Acopio.Domain.Tests.Datos;

namespace TrazaCafe.Acopio.Domain.Tests;

public sealed class LoteConformacionTests
{
    [Fact(DisplayName = "I1 · Un lote sin entregas no se puede conformar")]
    public void Lote_sin_entregas_es_rechazado()
    {
        var ex = Assert.Throws<DomainException>(() => Lote.Conformar(Guid.NewGuid(), [], Consentidas, Ahora));
        Assert.Contains("al menos una entrega", ex.Message);
    }

    [Fact(DisplayName = "I2 · Una entrega ya asignada no puede entrar a otro lote")]
    public void Entrega_ya_asignada_es_rechazada()
    {
        var entrega = Entrega(FincaA, 100m);
        _ = Lote(entrega);

        var ex = Assert.Throws<DomainException>(() => Lote(entrega));
        Assert.Contains("ya pertenecen a otro lote", ex.Message);
    }

    [Fact(DisplayName = "I2 · La misma entrega repetida en la solicitud es rechazada")]
    public void Entrega_repetida_en_la_solicitud_es_rechazada()
    {
        var entrega = Entrega(FincaA, 100m);
        Assert.Throws<DomainException>(() => Lote(entrega, entrega));
    }

    [Fact(DisplayName = "I3 · Balance de masa: kilos del lote = suma de sus entregas")]
    public void Kilos_del_lote_son_la_suma_de_las_entregas()
    {
        var e1 = Entrega(FincaA, 125.5m);
        var e2 = Entrega(FincaB, 80.25m);
        var e3 = Entrega(FincaA, 44.125m);

        var lote = Lote(e1, e2, e3);

        Assert.Equal(249.875m, lote.KilosTotales);
        Assert.Equal(lote.Entregas.Sum(e => e.Kilos), lote.KilosTotales);
        Assert.Equal(3, lote.Entregas.Count);
        Assert.Equal(2, lote.FincaIds.Count);
    }

    [Fact(DisplayName = "Conformar asigna cada entrega al lote y lo deja PendienteVerificacion")]
    public void Conformar_asigna_entregas_y_deja_pendiente()
    {
        var e1 = Entrega(FincaA, 10m);
        var e2 = Entrega(FincaB, 20m);

        var lote = Lote(e1, e2);

        Assert.Equal(EstadoLote.PendienteVerificacion, lote.Estado);
        Assert.Null(lote.VerificadoEn);
        Assert.Equal(lote.Id, e1.LoteId);
        Assert.Equal(lote.Id, e2.LoteId);
        Assert.StartsWith("LOT-20261031-", lote.Codigo);
    }

    [Fact(DisplayName = "Consentimiento · No se incluyen fincas sin autorización de uso de datos")]
    public void Finca_sin_consentimiento_es_rechazada()
    {
        var entrega = Entrega(FincaSinConsentimiento, 50m);

        var ex = Assert.Throws<DomainException>(() => Lote(entrega));
        Assert.Contains("sin autorización", ex.Message);
        Assert.False(entrega.EstaAsignada); // nada cambió
    }
}

public sealed class LoteVerificacionTests
{
    private static ResultadoVerificacionFinca Ok(Guid finca) => new(finca, true, "Sin pérdida de bosque");
    private static ResultadoVerificacionFinca NoConforme(Guid finca) => new(finca, false, "Pérdida posterior al 2020-12-31");

    [Fact(DisplayName = "Todas las fincas conformes → Declarable")]
    public void Todas_conformes_lo_vuelve_declarable()
    {
        var lote = Lote(Entrega(FincaA, 10m), Entrega(FincaB, 20m));

        lote.AplicarVerificacion([Ok(FincaA), Ok(FincaB)], Ahora.AddSeconds(2));

        Assert.Equal(EstadoLote.Declarable, lote.Estado);
        Assert.Equal(Ahora.AddSeconds(2), lote.VerificadoEn);
        Assert.Equal(2, lote.Verificaciones.Count);
    }

    [Fact(DisplayName = "I5 · Basta una finca no conforme para Bloquear el lote")]
    public void Una_no_conforme_bloquea()
    {
        var lote = Lote(Entrega(FincaA, 10m), Entrega(FincaB, 20m));

        lote.AplicarVerificacion([Ok(FincaA), NoConforme(FincaB)], Ahora);

        Assert.Equal(EstadoLote.Bloqueado, lote.Estado);
    }

    [Fact(DisplayName = "I4 · Un lote en estado final no admite otra verificación")]
    public void Estado_final_es_inmutable()
    {
        var lote = Lote(Entrega(FincaA, 10m));
        lote.AplicarVerificacion([Ok(FincaA)], Ahora);

        var ex = Assert.Throws<DomainException>(() => lote.AplicarVerificacion([NoConforme(FincaA)], Ahora));
        Assert.Contains("ya fue verificado", ex.Message);
        Assert.Equal(EstadoLote.Declarable, lote.Estado);
    }

    [Fact(DisplayName = "I5 · El resultado no puede referirse a fincas ajenas al lote")]
    public void Finca_ajena_es_rechazada()
    {
        var lote = Lote(Entrega(FincaA, 10m));

        Assert.Throws<DomainException>(() => lote.AplicarVerificacion([Ok(FincaA), Ok(FincaB)], Ahora));
        Assert.Equal(EstadoLote.PendienteVerificacion, lote.Estado);
    }

    [Fact(DisplayName = "I5 · El resultado debe cubrir todas las fincas del lote")]
    public void Resultado_incompleto_es_rechazado()
    {
        var lote = Lote(Entrega(FincaA, 10m), Entrega(FincaB, 20m));

        var ex = Assert.Throws<DomainException>(() => lote.AplicarVerificacion([Ok(FincaA)], Ahora));
        Assert.Contains("no cubre", ex.Message);
    }
}
