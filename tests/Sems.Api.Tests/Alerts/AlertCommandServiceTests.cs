using NSubstitute;
using Sems.Api.Modules.Alerts.Application;
using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Modules.Alerts.Domain.Repositories;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;
using Xunit;

namespace Sems.Api.Tests.Alerts;

/// <summary>
/// Evaluacion de la demanda de un local de 120 kW con aviso al 85 %, con los
/// repositorios y el bus de eventos simulados.
/// </summary>
public class AlertCommandServiceTests
{
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();

    private readonly IAlertRepository _alerts = Substitute.For<IAlertRepository>();
    private readonly IDemandRuleRepository _demandRules = Substitute.For<IDemandRuleRepository>();
    private readonly IInactivityRuleRepository _inactivityRules = Substitute.For<IInactivityRuleRepository>();
    private readonly IDomainEventBus _bus = Substitute.For<IDomainEventBus>();
    private readonly AlertCommandService _service;

    public AlertCommandServiceTests()
    {
        _alerts.SaveAsync(Arg.Any<Alert>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Alert>());
        _demandRules.FindActiveBySiteIdAsync(SiteId, Arg.Any<CancellationToken>())
            .Returns(new List<DemandRule> { Rule(85) });

        _service = new AlertCommandService(_alerts, Substitute.For<IThresholdRepository>(),
            _inactivityRules, _demandRules, Substitute.For<INotificationPreferenceRepository>(), _bus);
    }

    private static DemandRule Rule(double warningPercent) =>
        DemandRule.Create(SiteId, ManagerId, "Contracted power", 120, warningPercent, true);

    // ------------------------------------------------------ demanda medida

    [Theory]
    [InlineData(90)]
    [InlineData(101.9)]
    public async Task EvaluateDemandAsync_BelowTheThreshold_RaisesNoAlertNorEvent(double demand)
    {
        var raised = await _service.EvaluateDemandAsync(SiteId, demand);

        Assert.Empty(raised);
        await _alerts.DidNotReceive().SaveAsync(Arg.Any<Alert>(), Arg.Any<CancellationToken>());
        _bus.DidNotReceive().Publish(Arg.Any<IDomainEvent>());
    }

    [Fact]
    public async Task EvaluateDemandAsync_102Kw_RaisesWarningWith18KwOfHeadroom()
    {
        var raised = await _service.EvaluateDemandAsync(SiteId, 102);

        var alert = Assert.Single(raised);
        Assert.Equal("WARNING", alert.Severity);
        Assert.Equal("DEMAND", alert.AlertType);
        Assert.Contains("18 kW of headroom left", alert.Message);
        Assert.Equal(ManagerId, alert.UserId);
    }

    [Fact]
    public async Task EvaluateDemandAsync_135Kw_RaisesCriticalAndPublishesAlertTriggered()
    {
        var raised = await _service.EvaluateDemandAsync(SiteId, 135);

        var alert = Assert.Single(raised);
        Assert.Equal("CRITICAL", alert.Severity);
        Assert.Contains("15 kW above the 120 kW contracted", alert.Message);
        _bus.Received(1).Publish(Arg.Is<DomainEvents.AlertTriggered>(e =>
            e.AlertId == alert.AlertId && e.UserId == ManagerId && e.Severity == "CRITICAL"
            && e.Message.Contains("15 kW above the 120 kW contracted")));
    }

    [Fact]
    public async Task EvaluateDemandAsync_TwoRulesBroken_RaisesOneAlertPerRule()
    {
        _demandRules.FindActiveBySiteIdAsync(SiteId, Arg.Any<CancellationToken>())
            .Returns(new List<DemandRule> { Rule(85), Rule(75) });

        // 110 kW supera el aviso de las dos reglas: 102 kW (85 %) y 90 kW (75 %).
        var raised = await _service.EvaluateDemandAsync(SiteId, 110);

        Assert.Equal(2, raised.Count);
        Assert.All(raised, a => Assert.Equal("WARNING", a.Severity));
        _bus.Received(2).Publish(Arg.Any<DomainEvents.AlertTriggered>());
    }

    [Fact]
    public async Task EvaluateDemandAsync_SiteWithoutRules_RaisesNothing()
    {
        var siteWithoutRules = Guid.NewGuid();
        _demandRules.FindActiveBySiteIdAsync(siteWithoutRules, Arg.Any<CancellationToken>())
            .Returns(new List<DemandRule>());

        var raised = await _service.EvaluateDemandAsync(siteWithoutRules, 500);

        Assert.Empty(raised);
    }

    // ----------------------------------------------------------- otras reglas

    [Fact]
    public async Task CreateAlertAsync_AnyAlert_PublishesAlertTriggeredSoItIsEmailed()
    {
        var userId = Guid.NewGuid();

        var alert = await _service.CreateAlertAsync(userId, Guid.NewGuid(), null, null,
            "threshold_exceeded", "Consumption above threshold", "1500 > 1000 power_watts", "high",
            null, null);

        _bus.Received(1).Publish(Arg.Is<DomainEvents.AlertTriggered>(e =>
            e.AlertId == alert.AlertId && e.UserId == userId));
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownAlert_ThrowsNotFound()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.UpdateStatusAsync(Guid.NewGuid(), "resolved", null));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public async Task CreateInactivityRuleAsync_NonPositiveMinutes_ThrowsValidationError(int minutes)
    {
        var error = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateInactivityRuleAsync(Guid.NewGuid(), Guid.NewGuid(), "Silent", minutes, true));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        await _inactivityRules.DidNotReceive().SaveAsync(Arg.Any<InactivityRule>(),
            Arg.Any<CancellationToken>());
    }
}
