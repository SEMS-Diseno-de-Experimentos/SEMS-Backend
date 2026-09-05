using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sems.Api.Modules.Devices.Domain.Model;
using Sems.Api.Modules.Devices.Domain.Repositories;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Devices.Infrastructure;

/// <summary>
/// Mapeo a tablas del modulo de dispositivos.
///
/// <para>Vive en infraestructura porque es un detalle de persistencia: el
/// dominio no debe depender de EF Core. El nombre de tabla lleva el prefijo
/// <c>dm_</c> para aislar el modulo dentro de la base compartida.</para>
/// </summary>
public sealed class DeviceConfig : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("dm_devices");
        builder.HasKey(d => d.DeviceId);
        builder.Property(d => d.ExternalDeviceCode).HasMaxLength(120).IsRequired();
        builder.Property(d => d.DeviceName).HasMaxLength(160).IsRequired();
        builder.Property(d => d.DeviceType).HasMaxLength(80).IsRequired();
        builder.Property(d => d.Brand).HasMaxLength(120);
        builder.Property(d => d.Model).HasMaxLength(120);
        builder.Property(d => d.ConnectionProtocol).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => d.ExternalDeviceCode).IsUnique();
    }
}

public sealed class DeviceBindingConfig : IEntityTypeConfiguration<DeviceBinding>
{
    public void Configure(EntityTypeBuilder<DeviceBinding> builder)
    {
        builder.ToTable("dm_device_bindings");
        builder.HasKey(b => b.BindingId);
        builder.Property(b => b.BindingStatus).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(b => b.DeviceId);
        builder.HasIndex(b => b.UserId);
    }
}

public sealed class DeviceConfigurationConfig : IEntityTypeConfiguration<DeviceConfiguration>
{
    public void Configure(EntityTypeBuilder<DeviceConfiguration> builder)
    {
        builder.ToTable("dm_device_configurations");
        builder.HasKey(c => c.ConfigurationId);
        builder.Property(c => c.ConfigKey).HasMaxLength(120).IsRequired();
        builder.HasIndex(c => new { c.DeviceId, c.ConfigKey }).IsUnique();
    }
}

public sealed class DeviceEventConfig : IEntityTypeConfiguration<DeviceEvent>
{
    public void Configure(EntityTypeBuilder<DeviceEvent> builder)
    {
        builder.ToTable("dm_device_events");
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventType).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => e.DeviceId);
    }
}

// ---------------------------------------------------------------- adaptadores

/// <summary>Implementacion del puerto sobre EF Core.</summary>
public sealed class DeviceRepository : IDeviceRepository
{
    private readonly SemsDbContext _db;

    public DeviceRepository(SemsDbContext db) => _db = db;

    public async Task<Device> SaveAsync(Device device, CancellationToken ct = default)
    {
        // Un agregado ya rastreado se actualiza solo; uno nuevo hay que anadirlo.
        if (_db.Entry(device).State == EntityState.Detached)
        {
            _db.Add(device);
        }
        await _db.SaveChangesAsync(ct);
        return device;
    }

    public Task<Device?> FindByIdAsync(Guid deviceId, CancellationToken ct = default) =>
        _db.Set<Device>().FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);

    public Task<List<Device>> FindAllAsync(CancellationToken ct = default) =>
        _db.Set<Device>().OrderByDescending(d => d.RegisteredAt).ToListAsync(ct);

    public Task<List<Device>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<Device>().Where(d => d.UserId == userId)
            .OrderByDescending(d => d.RegisteredAt).ToListAsync(ct);

    public Task<bool> ExistsByExternalCodeAsync(string externalDeviceCode, CancellationToken ct = default) =>
        _db.Set<Device>().AnyAsync(d => d.ExternalDeviceCode == externalDeviceCode, ct);
}

public sealed class DeviceBindingRepository : IDeviceBindingRepository
{
    private readonly SemsDbContext _db;

    public DeviceBindingRepository(SemsDbContext db) => _db = db;

    public async Task<DeviceBinding> SaveAsync(DeviceBinding binding, CancellationToken ct = default)
    {
        if (_db.Entry(binding).State == EntityState.Detached)
        {
            _db.Add(binding);
        }
        await _db.SaveChangesAsync(ct);
        return binding;
    }

    public Task<DeviceBinding?> FindByIdAsync(Guid bindingId, CancellationToken ct = default) =>
        _db.Set<DeviceBinding>().FirstOrDefaultAsync(b => b.BindingId == bindingId, ct);

    public Task<List<DeviceBinding>> FindByDeviceIdAsync(Guid deviceId, CancellationToken ct = default) =>
        _db.Set<DeviceBinding>().Where(b => b.DeviceId == deviceId).ToListAsync(ct);

    public Task<List<DeviceBinding>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<DeviceBinding>().Where(b => b.UserId == userId).ToListAsync(ct);

    public Task<DeviceBinding?> FindActiveByDeviceIdAsync(Guid deviceId, CancellationToken ct = default) =>
        _db.Set<DeviceBinding>()
            .FirstOrDefaultAsync(b => b.DeviceId == deviceId
                                      && b.BindingStatus == BindingStatus.LINKED, ct);
}

public sealed class DeviceConfigurationRepository : IDeviceConfigurationRepository
{
    private readonly SemsDbContext _db;

    public DeviceConfigurationRepository(SemsDbContext db) => _db = db;

    public async Task<DeviceConfiguration> SaveAsync(DeviceConfiguration configuration,
        CancellationToken ct = default)
    {
        if (_db.Entry(configuration).State == EntityState.Detached)
        {
            _db.Add(configuration);
        }
        await _db.SaveChangesAsync(ct);
        return configuration;
    }

    public Task<DeviceConfiguration?> FindByIdAsync(Guid configurationId, CancellationToken ct = default) =>
        _db.Set<DeviceConfiguration>()
            .FirstOrDefaultAsync(c => c.ConfigurationId == configurationId, ct);

    public Task<List<DeviceConfiguration>> FindByDeviceIdAsync(Guid deviceId, CancellationToken ct = default) =>
        _db.Set<DeviceConfiguration>().Where(c => c.DeviceId == deviceId).ToListAsync(ct);

    public Task<DeviceConfiguration?> FindByDeviceIdAndKeyAsync(Guid deviceId, string configKey,
        CancellationToken ct = default) =>
        _db.Set<DeviceConfiguration>()
            .FirstOrDefaultAsync(c => c.DeviceId == deviceId && c.ConfigKey == configKey, ct);
}

public sealed class DeviceEventRepository : IDeviceEventRepository
{
    private readonly SemsDbContext _db;

    public DeviceEventRepository(SemsDbContext db) => _db = db;

    public async Task<DeviceEvent> SaveAsync(DeviceEvent deviceEvent, CancellationToken ct = default)
    {
        _db.Add(deviceEvent);
        await _db.SaveChangesAsync(ct);
        return deviceEvent;
    }

    public Task<List<DeviceEvent>> FindByDeviceIdAsync(Guid deviceId, CancellationToken ct = default) =>
        _db.Set<DeviceEvent>().Where(e => e.DeviceId == deviceId)
            .OrderByDescending(e => e.OccurredAt).ToListAsync(ct);
}
