using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sems.Api.Modules.Energy.Domain.Model;
using Sems.Api.Modules.Energy.Domain.Repositories;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Energy.Infrastructure;

public sealed class EnergyMeterConfig : IEntityTypeConfiguration<EnergyMeter>
{
    public void Configure(EntityTypeBuilder<EnergyMeter> builder)
    {
        builder.ToTable("em_energy_meters");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.UserId).HasMaxLength(80).IsRequired();
        builder.Property(m => m.MeterSerial).HasMaxLength(120).IsRequired();
        builder.Property(m => m.Model).HasMaxLength(120);
        builder.Property(m => m.Brand).HasMaxLength(120);
        builder.Property(m => m.Location).HasMaxLength(160);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.FirmwareVersion).HasMaxLength(40);
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.MeterSerial).IsUnique();
    }
}

/// <summary>
/// Lecturas del medidor. Sustituye a la coleccion de MongoDB.
///
/// <para>Los indices compuestos por dispositivo y marca de tiempo son los que
/// sostienen las consultas de historial y de ultima lectura, que son las mas
/// frecuentes.</para>
/// </summary>
public sealed class EnergyReadingConfig : IEntityTypeConfiguration<EnergyReading>
{
    public void Configure(EntityTypeBuilder<EnergyReading> builder)
    {
        builder.ToTable("em_energy_readings");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).HasMaxLength(80).IsRequired();
        builder.Property(r => r.MeterId).HasMaxLength(80).IsRequired();
        builder.Property(r => r.DeviceId).HasMaxLength(80);
        builder.Property(r => r.ReadingType).HasMaxLength(40);
        builder.Property(r => r.Phase).HasMaxLength(20);
        builder.HasIndex(r => new { r.UserId, r.Timestamp });
        builder.HasIndex(r => new { r.DeviceId, r.Timestamp });
        builder.HasIndex(r => new { r.MeterId, r.Timestamp });
    }
}

public sealed class DeviceConsumptionConfig : IEntityTypeConfiguration<DeviceConsumption>
{
    public void Configure(EntityTypeBuilder<DeviceConsumption> builder)
    {
        builder.ToTable("em_device_consumptions");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.UserId).HasMaxLength(80).IsRequired();
        builder.Property(c => c.DeviceId).HasMaxLength(80).IsRequired();
        builder.Property(c => c.DeviceName).HasMaxLength(160);
        builder.Property(c => c.MeterId).HasMaxLength(80);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.DeviceId);
    }
}

public sealed class ConsumptionAlertConfig : IEntityTypeConfiguration<ConsumptionAlert>
{
    public void Configure(EntityTypeBuilder<ConsumptionAlert> builder)
    {
        builder.ToTable("em_consumption_alerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).HasMaxLength(80).IsRequired();
        builder.Property(a => a.DeviceId).HasMaxLength(80);
        builder.Property(a => a.MeterId).HasMaxLength(80);
        builder.Property(a => a.AlertType).HasConversion<string>().HasMaxLength(40);
        builder.Property(a => a.Severity).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => a.UserId);
    }
}

// ---------------------------------------------------------------- adaptadores

public sealed class EnergyMeterRepository : IEnergyMeterRepository
{
    private readonly SemsDbContext _db;

    public EnergyMeterRepository(SemsDbContext db) => _db = db;

    public async Task<EnergyMeter> SaveAsync(EnergyMeter meter, CancellationToken ct = default)
    {
        if (_db.Entry(meter).State == EntityState.Detached)
        {
            _db.Add(meter);
        }
        await _db.SaveChangesAsync(ct);
        return meter;
    }

    public Task<EnergyMeter?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Set<EnergyMeter>().FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<EnergyMeter?> FindBySerialAsync(string meterSerial, CancellationToken ct = default) =>
        _db.Set<EnergyMeter>().FirstOrDefaultAsync(m => m.MeterSerial == meterSerial, ct);

    public Task<List<EnergyMeter>> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        _db.Set<EnergyMeter>().Where(m => m.UserId == userId)
            .OrderByDescending(m => m.RegisteredAt).ToListAsync(ct);
}

public sealed class EnergyReadingRepository : IEnergyReadingRepository
{
    private readonly SemsDbContext _db;

    public EnergyReadingRepository(SemsDbContext db) => _db = db;

    public async Task<EnergyReading> SaveAsync(EnergyReading reading, CancellationToken ct = default)
    {
        _db.Add(reading);
        await _db.SaveChangesAsync(ct);
        return reading;
    }

    public Task<EnergyReading?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Set<EnergyReading>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<EnergyReading>> FindByUserIdAsync(string userId, int limit,
        CancellationToken ct = default) =>
        _db.Set<EnergyReading>().Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Timestamp).Take(limit).ToListAsync(ct);

    public Task<List<EnergyReading>> FindByDeviceIdAsync(string deviceId, int limit, int skip,
        CancellationToken ct = default) =>
        _db.Set<EnergyReading>().Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.Timestamp).Skip(skip).Take(limit).ToListAsync(ct);

    public Task<List<EnergyReading>> FindByRangeAsync(string userId, DateTime from, DateTime to,
        CancellationToken ct = default) =>
        _db.Set<EnergyReading>()
            .Where(r => r.UserId == userId && r.Timestamp >= from && r.Timestamp <= to)
            .OrderBy(r => r.Timestamp).ToListAsync(ct);

    public Task<EnergyReading?> FindLatestByMeterAsync(string meterId, CancellationToken ct = default) =>
        _db.Set<EnergyReading>().Where(r => r.MeterId == meterId)
            .OrderByDescending(r => r.Timestamp).FirstOrDefaultAsync(ct);

    public Task<EnergyReading?> FindLatestByDeviceAsync(string deviceId, CancellationToken ct = default) =>
        _db.Set<EnergyReading>().Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.Timestamp).FirstOrDefaultAsync(ct);
}

public sealed class DeviceConsumptionRepository : IDeviceConsumptionRepository
{
    private readonly SemsDbContext _db;

    public DeviceConsumptionRepository(SemsDbContext db) => _db = db;

    public async Task<DeviceConsumption> SaveAsync(DeviceConsumption consumption,
        CancellationToken ct = default)
    {
        if (_db.Entry(consumption).State == EntityState.Detached)
        {
            _db.Add(consumption);
        }
        await _db.SaveChangesAsync(ct);
        return consumption;
    }

    public Task<DeviceConsumption?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Set<DeviceConsumption>().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<DeviceConsumption>> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        _db.Set<DeviceConsumption>().Where(c => c.UserId == userId)
            .OrderByDescending(c => c.PeriodEnd).ToListAsync(ct);

    public Task<List<DeviceConsumption>> FindTopByUserIdAsync(string userId, int limit,
        CancellationToken ct = default) =>
        _db.Set<DeviceConsumption>().Where(c => c.UserId == userId)
            .OrderByDescending(c => c.TotalKwh).Take(limit).ToListAsync(ct);
}

public sealed class ConsumptionAlertRepository : IConsumptionAlertRepository
{
    private readonly SemsDbContext _db;

    public ConsumptionAlertRepository(SemsDbContext db) => _db = db;

    public async Task<ConsumptionAlert> SaveAsync(ConsumptionAlert alert, CancellationToken ct = default)
    {
        if (_db.Entry(alert).State == EntityState.Detached)
        {
            _db.Add(alert);
        }
        await _db.SaveChangesAsync(ct);
        return alert;
    }

    public Task<ConsumptionAlert?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Set<ConsumptionAlert>().FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<ConsumptionAlert>> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        _db.Set<ConsumptionAlert>().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt).ToListAsync(ct);

    public Task<List<ConsumptionAlert>> FindUnreadByUserIdAsync(string userId,
        CancellationToken ct = default) =>
        _db.Set<ConsumptionAlert>().Where(a => a.UserId == userId && !a.IsRead)
            .OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
}
