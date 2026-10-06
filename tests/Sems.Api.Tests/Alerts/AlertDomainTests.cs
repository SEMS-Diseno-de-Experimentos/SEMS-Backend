using System.Text.Json;
using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Alerts;

/// <summary>
/// Evaluacion de umbrales y de inactividad, y estado de las alertas.
///
/// <para>Es la logica que decide si al usuario le llega un correo. Un fallo aqui
/// no rompe nada visible: simplemente el aviso no se envia, o se envia a
/// destiempo, que es peor porque nadie lo reporta.</para>
/// </summary>
public class AlertDomainTests
{
    [Fact]
    public void Serialize_Operator_UsesTheSymbolNotTheName()
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
    public void ToOperator_SymbolOrName_IsAccepted()
    {
        Assert.Equal(Operator.GREATER_THAN, OperatorExtensions.ToOperator(">"));
        Assert.Equal(Operator.GREATER_THAN_OR_EQUAL, OperatorExtensions.ToOperator(">="));
        Assert.Equal(Operator.EQUAL, OperatorExtensions.ToOperator("=="));
        // Los umbrales guardados antes traen el nombre del enum.
        Assert.Equal(Operator.LESS_THAN, OperatorExtensions.ToOperator("less_than"));
    }

    [Fact]
    public void ToOperator_UnknownOperator_ThrowsValidationError()
    {
        var error = Assert.Throws<AppException>(() => OperatorExtensions.ToOperator("=>"));
        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Fact]
    public void IsBreachedBy_StrictGreaterThan_OnlyBreaksWhenTheComparisonHolds()
    {
        var threshold = AlertThreshold.Create(Guid.NewGuid(), Guid.NewGuid(),
            "Consumo alto", "power_watts", Operator.GREATER_THAN, 1000, true);

        Assert.True(threshold.IsBreachedBy(1500));
        Assert.False(threshold.IsBreachedBy(1000));   // estricto: 1000 no es mayor que 1000
        Assert.False(threshold.IsBreachedBy(500));
    }

    [Fact]
    public void IsBreachedBy_DeactivatedThreshold_NeverBreaks()
    {
        var threshold = AlertThreshold.Create(Guid.NewGuid(), null,
            "Consumo alto", "power_watts", Operator.GREATER_THAN, 1000, true);

        threshold.Deactivate();

        Assert.False(threshold.Active);
        Assert.False(threshold.IsBreachedBy(99999));
    }

    [Fact]
    public void IsInactive_MeasuredFromTheLastSignal_CountsTheLimitAsInactive()
    {
        var rule = InactivityRule.Create(Guid.NewGuid(), Guid.NewGuid(), "Sin reportar", 60, true);
        var now = DateTime.UtcNow;

        Assert.True(rule.IsInactive(now.AddMinutes(-90), now));
        Assert.True(rule.IsInactive(now.AddMinutes(-60), now));   // el limite ya cuenta
        Assert.False(rule.IsInactive(now.AddMinutes(-10), now));
    }

    [Fact]
    public void IsInactive_WithoutLastSignalOrWithNonPositiveLimit_NeverFires()
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
    public void UpdateStatus_Resolved_StampsTheDateEvenIfNotSent()
    {
        var alert = Alert.Raise(Guid.NewGuid(), Guid.NewGuid(), null, null,
            "threshold", "Consumo alto", "El dispositivo supero el umbral", "high", null, null);

        Assert.Equal(Alert.StatusActive, alert.Status);
        Assert.Null(alert.ResolvedAt);

        alert.UpdateStatus(Alert.StatusResolved, null);

        Assert.Equal(Alert.StatusResolved, alert.Status);
        Assert.NotNull(alert.ResolvedAt);
    }

    [Fact]
    public void UpdateStatus_ResolvedWithADate_KeepsTheDateSent()
    {
        var alert = Alert.Raise(Guid.NewGuid(), null, null, null, "DEMAND", "Demand", "msg",
            "WARNING", null, null);
        var resolvedAt = new DateTime(2026, 10, 5, 22, 15, 0, DateTimeKind.Utc);

        alert.UpdateStatus(Alert.StatusResolved, resolvedAt);

        Assert.Equal(resolvedAt, alert.ResolvedAt);
    }

    [Theory]
    [InlineData(Operator.GREATER_THAN_OR_EQUAL, 1000, true)]
    [InlineData(Operator.LESS_THAN, 999, true)]
    [InlineData(Operator.LESS_THAN_OR_EQUAL, 1001, false)]
    [InlineData(Operator.EQUAL, 1000, true)]
    public void Test_EachOperator_ComparesAgainstTheThreshold(Operator op, double value, bool expected)
    {
        Assert.Equal(expected, op.Test(value, 1000));
    }
}
