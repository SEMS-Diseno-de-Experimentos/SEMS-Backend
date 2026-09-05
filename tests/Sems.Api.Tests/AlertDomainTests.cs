using System.Text.Json;
using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests;

/// <summary>
/// Evaluacion de umbrales y de inactividad.
///
/// <para>Es la logica que decide si al usuario le llega un correo. Un fallo aqui
/// no rompe nada visible: simplemente el aviso no se envia, o se envia a
/// destiempo, que es peor porque nadie lo reporta.</para>
/// </summary>
public class AlertDomainTests
{
    [Fact]
    public void El_operador_se_serializa_como_simbolo_no_como_nombre()
    {
        // La interfaz muestra ">", no "GREATER_THAN".
        //
        // Se comprueba sobre el valor deserializado y no sobre el texto crudo a
        // proposito: System.Text.Json escapa "<" y ">" como < y > por
        // seguridad frente a HTML. Es JSON valido y JSON.parse lo devuelve como
        // el simbolo, asi que el contrato con el frontend se cumple; comparar el
        // texto tal cual solo estaria probando el escapado del serializador.
        Assert.Equal(">", JsonSerializer.Deserialize<string>(
            JsonSerializer.Serialize(Operator.GREATER_THAN)));
        Assert.Equal("<=", JsonSerializer.Deserialize<string>(
            JsonSerializer.Serialize(Operator.LESS_THAN_OR_EQUAL)));
        Assert.Equal("==", JsonSerializer.Deserialize<string>(
            JsonSerializer.Serialize(Operator.EQUAL)));

        // Y el viaje de ida y vuelta completo, que es lo que hace el cliente.
        Assert.Equal(Operator.GREATER_THAN_OR_EQUAL, JsonSerializer.Deserialize<Operator>(
            JsonSerializer.Serialize(Operator.GREATER_THAN_OR_EQUAL)));
    }

    [Fact]
    public void El_operador_se_acepta_por_simbolo_y_por_nombre()
    {
        Assert.Equal(Operator.GREATER_THAN, OperatorExtensions.ToOperator(">"));
        Assert.Equal(Operator.GREATER_THAN_OR_EQUAL, OperatorExtensions.ToOperator(">="));
        Assert.Equal(Operator.EQUAL, OperatorExtensions.ToOperator("=="));
        // Los umbrales guardados antes traen el nombre del enum.
        Assert.Equal(Operator.LESS_THAN, OperatorExtensions.ToOperator("less_than"));
    }

    [Fact]
    public void Un_operador_desconocido_se_rechaza()
    {
        var error = Assert.Throws<AppException>(() => OperatorExtensions.ToOperator("=>"));
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Fact]
    public void El_umbral_solo_se_rompe_cuando_la_comparacion_se_cumple()
    {
        var threshold = AlertThreshold.Create(Guid.NewGuid(), Guid.NewGuid(),
            "Consumo alto", "power_watts", Operator.GREATER_THAN, 1000, true);

        Assert.True(threshold.IsBreachedBy(1500));
        Assert.False(threshold.IsBreachedBy(1000));   // estricto: 1000 no es mayor que 1000
        Assert.False(threshold.IsBreachedBy(500));
    }

    [Fact]
    public void Un_umbral_desactivado_no_dispara_nada()
    {
        var threshold = AlertThreshold.Create(Guid.NewGuid(), null,
            "Consumo alto", "power_watts", Operator.GREATER_THAN, 1000, true);

        threshold.Deactivate();

        Assert.False(threshold.Active);
        Assert.False(threshold.IsBreachedBy(99999));
    }

    [Fact]
    public void La_regla_de_inactividad_se_mide_desde_la_ultima_senal()
    {
        var rule = InactivityRule.Create(Guid.NewGuid(), Guid.NewGuid(), "Sin reportar", 60, true);
        var now = DateTime.UtcNow;

        Assert.True(rule.IsInactive(now.AddMinutes(-90), now));
        Assert.True(rule.IsInactive(now.AddMinutes(-60), now));   // el limite ya cuenta
        Assert.False(rule.IsInactive(now.AddMinutes(-10), now));
    }

    [Fact]
    public void Sin_ultima_senal_o_con_umbral_no_positivo_la_regla_no_dispara()
    {
        var now = DateTime.UtcNow;

        // Un dispositivo que nunca reporto no puede considerarse "inactivo desde".
        var rule = InactivityRule.Create(Guid.NewGuid(), null, "Sin reportar", 60, true);
        Assert.False(rule.IsInactive(null, now));

        // Sin esta guarda, un umbral de cero marcaria todo como inactivo siempre.
        var zero = InactivityRule.Create(Guid.NewGuid(), null, "Mal configurada", 0, true);
        Assert.False(zero.IsInactive(now.AddDays(-30), now));
    }

    [Fact]
    public void Al_resolver_una_alerta_se_sella_la_fecha_aunque_no_la_manden()
    {
        var alert = Alert.Raise(Guid.NewGuid(), Guid.NewGuid(), null, null,
            "threshold", "Consumo alto", "El dispositivo supero el umbral", "high", null, null);

        Assert.Equal(Alert.StatusActive, alert.Status);
        Assert.Null(alert.ResolvedAt);

        alert.UpdateStatus(Alert.StatusResolved, null);

        Assert.Equal(Alert.StatusResolved, alert.Status);
        Assert.NotNull(alert.ResolvedAt);
    }
}
