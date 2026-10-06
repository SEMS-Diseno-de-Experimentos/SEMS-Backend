using Sems.Api.Modules.Alerts.Domain.Model;

namespace Sems.Api.Modules.Alerts.Domain.Repositories;

/// <summary>Puertos de salida del modulo de alertas.</summary>
public interface IAlertRepository
{
    Task<Alert> SaveAsync(Alert alert, CancellationToken ct = default);
    Task<Alert?> FindByIdAsync(Guid alertId, CancellationToken ct = default);
    Task<List<Alert>> FindAllAsync(CancellationToken ct = default);
    Task<List<Alert>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
}

public interface IThresholdRepository
{
    Task<AlertThreshold> SaveAsync(AlertThreshold threshold, CancellationToken ct = default);
    Task<AlertThreshold?> FindByIdAsync(Guid thresholdId, CancellationToken ct = default);
    Task<List<AlertThreshold>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Umbrales activos de un dispositivo; los usa la evaluacion automatica.</summary>
    Task<List<AlertThreshold>> FindActiveByDeviceIdAsync(Guid? deviceId, CancellationToken ct = default);
}

public interface IDemandRuleRepository
{
    Task<DemandRule> SaveAsync(DemandRule rule, CancellationToken ct = default);
    Task<DemandRule?> FindByIdAsync(Guid demandRuleId, CancellationToken ct = default);
    /// <summary>Reglas activas de un local; las usa la evaluacion de demanda.</summary>
    Task<List<DemandRule>> FindActiveBySiteIdAsync(Guid siteId, CancellationToken ct = default);
    Task<List<DemandRule>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
}

public interface IInactivityRuleRepository
{
    Task<InactivityRule> SaveAsync(InactivityRule rule, CancellationToken ct = default);
    Task<List<InactivityRule>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<InactivityRule>> FindAllActiveAsync(CancellationToken ct = default);
}

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference> SaveAsync(NotificationPreference preference, CancellationToken ct = default);
    Task<List<NotificationPreference>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<NotificationPreference?> FindByUserIdAndChannelAsync(Guid userId, string channel, CancellationToken ct = default);
}

public interface INotificationLogRepository
{
    Task<NotificationLog> SaveAsync(NotificationLog log, CancellationToken ct = default);
    Task<List<NotificationLog>> FindByAlertIdAsync(Guid alertId, CancellationToken ct = default);
}
