using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Modules.Alerts.Domain.Repositories;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Alerts.Infrastructure;

public sealed class AlertConfig : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> b)
    {
        b.ToTable("al_alerts");
        b.HasKey(a => a.AlertId);
        b.Property(a => a.AlertType).HasMaxLength(80);
        b.Property(a => a.Title).HasMaxLength(200);
        b.Property(a => a.Message).HasColumnType("text");
        b.Property(a => a.Severity).HasMaxLength(20);
        b.Property(a => a.Status).HasMaxLength(20);
        b.HasIndex(a => a.UserId);
        b.HasIndex(a => a.DeviceId);
    }
}

public sealed class ThresholdConfig : IEntityTypeConfiguration<AlertThreshold>
{
    public void Configure(EntityTypeBuilder<AlertThreshold> b)
    {
        b.ToTable("al_thresholds");
        b.HasKey(t => t.ThresholdId);
        b.Property(t => t.ThresholdName).HasMaxLength(160);
        b.Property(t => t.Metric).HasMaxLength(80);
        b.Property(t => t.Operator).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(t => t.UserId);
        b.HasIndex(t => t.DeviceId);
    }
}

public sealed class InactivityRuleConfig : IEntityTypeConfiguration<InactivityRule>
{
    public void Configure(EntityTypeBuilder<InactivityRule> b)
    {
        b.ToTable("al_inactivity_rules");
        b.HasKey(r => r.InactivityRuleId);
        b.Property(r => r.RuleName).HasMaxLength(160);
        b.HasIndex(r => r.UserId);
    }
}

public sealed class PreferenceConfig : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> b)
    {
        b.ToTable("al_notification_preferences");
        b.HasKey(p => p.PreferenceId);
        b.Property(p => p.Channel).HasMaxLength(40).IsRequired();
        b.Property(p => p.MinSeverity).HasMaxLength(20);
        b.HasIndex(p => new { p.UserId, p.Channel }).IsUnique();
    }
}

public sealed class NotificationLogConfig : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> b)
    {
        b.ToTable("al_notification_logs");
        b.HasKey(l => l.NotificationId);
        b.Property(l => l.Channel).HasMaxLength(40);
        b.Property(l => l.Recipient).HasMaxLength(200);
        b.Property(l => l.Status).HasMaxLength(20);
        b.Property(l => l.ErrorMessage).HasColumnType("text");
        b.HasIndex(l => l.AlertId);
    }
}

// ---------------------------------------------------------------- adaptadores

public sealed class AlertRepository : IAlertRepository
{
    private readonly SemsDbContext _db;
    public AlertRepository(SemsDbContext db) => _db = db;

    public async Task<Alert> SaveAsync(Alert a, CancellationToken ct = default)
    {
        if (_db.Entry(a).State == EntityState.Detached) _db.Add(a);
        await _db.SaveChangesAsync(ct);
        return a;
    }

    public Task<Alert?> FindByIdAsync(Guid alertId, CancellationToken ct = default) =>
        _db.Set<Alert>().FirstOrDefaultAsync(a => a.AlertId == alertId, ct);

    public Task<List<Alert>> FindAllAsync(CancellationToken ct = default) =>
        _db.Set<Alert>().OrderByDescending(a => a.TriggeredAt).ToListAsync(ct);

    public Task<List<Alert>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<Alert>().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.TriggeredAt).ToListAsync(ct);
}

public sealed class ThresholdRepository : IThresholdRepository
{
    private readonly SemsDbContext _db;
    public ThresholdRepository(SemsDbContext db) => _db = db;

    public async Task<AlertThreshold> SaveAsync(AlertThreshold t, CancellationToken ct = default)
    {
        if (_db.Entry(t).State == EntityState.Detached) _db.Add(t);
        await _db.SaveChangesAsync(ct);
        return t;
    }

    public Task<AlertThreshold?> FindByIdAsync(Guid thresholdId, CancellationToken ct = default) =>
        _db.Set<AlertThreshold>().FirstOrDefaultAsync(t => t.ThresholdId == thresholdId, ct);

    public Task<List<AlertThreshold>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<AlertThreshold>().Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

    public Task<List<AlertThreshold>> FindActiveByDeviceIdAsync(Guid? deviceId,
        CancellationToken ct = default) =>
        deviceId is null
            ? Task.FromResult(new List<AlertThreshold>())
            : _db.Set<AlertThreshold>().Where(t => t.DeviceId == deviceId && t.Active).ToListAsync(ct);
}

public sealed class InactivityRuleRepository : IInactivityRuleRepository
{
    private readonly SemsDbContext _db;
    public InactivityRuleRepository(SemsDbContext db) => _db = db;

    public async Task<InactivityRule> SaveAsync(InactivityRule r, CancellationToken ct = default)
    {
        if (_db.Entry(r).State == EntityState.Detached) _db.Add(r);
        await _db.SaveChangesAsync(ct);
        return r;
    }

    public Task<List<InactivityRule>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<InactivityRule>().Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public Task<List<InactivityRule>> FindAllActiveAsync(CancellationToken ct = default) =>
        _db.Set<InactivityRule>().Where(r => r.Active).ToListAsync(ct);
}

public sealed class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly SemsDbContext _db;
    public NotificationPreferenceRepository(SemsDbContext db) => _db = db;

    public async Task<NotificationPreference> SaveAsync(NotificationPreference p,
        CancellationToken ct = default)
    {
        if (_db.Entry(p).State == EntityState.Detached) _db.Add(p);
        await _db.SaveChangesAsync(ct);
        return p;
    }

    public Task<List<NotificationPreference>> FindByUserIdAsync(Guid userId,
        CancellationToken ct = default) =>
        _db.Set<NotificationPreference>().Where(p => p.UserId == userId).ToListAsync(ct);

    public Task<NotificationPreference?> FindByUserIdAndChannelAsync(Guid userId, string channel,
        CancellationToken ct = default) =>
        _db.Set<NotificationPreference>()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Channel == channel, ct);
}

public sealed class NotificationLogRepository : INotificationLogRepository
{
    private readonly SemsDbContext _db;
    public NotificationLogRepository(SemsDbContext db) => _db = db;

    public async Task<NotificationLog> SaveAsync(NotificationLog l, CancellationToken ct = default)
    {
        _db.Add(l);
        await _db.SaveChangesAsync(ct);
        return l;
    }

    public Task<List<NotificationLog>> FindByAlertIdAsync(Guid alertId, CancellationToken ct = default) =>
        _db.Set<NotificationLog>().Where(l => l.AlertId == alertId)
            .OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
}
