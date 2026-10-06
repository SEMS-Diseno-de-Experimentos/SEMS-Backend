using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sems.Api.Modules.Analytics.Domain.Model;
using Sems.Api.Modules.Analytics.Domain.Repositories;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Analytics.Infrastructure;

public sealed class BillPredictionConfig : IEntityTypeConfiguration<BillPrediction>
{
    public void Configure(EntityTypeBuilder<BillPrediction> b)
    {
        b.ToTable("an_bill_predictions");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(80).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(10);
        b.HasIndex(x => x.UserId);
    }
}

public sealed class RecommendationConfig : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> b)
    {
        b.ToTable("an_recommendations");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(80).IsRequired();
        b.Property(x => x.DeviceId).HasMaxLength(80);
        b.Property(x => x.RecommendationType).HasMaxLength(80);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Currency).HasMaxLength(10);
        b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => x.UserId);
    }
}

public sealed class AnomalyConfig : IEntityTypeConfiguration<Anomaly>
{
    public void Configure(EntityTypeBuilder<Anomaly> b)
    {
        b.ToTable("an_anomalies");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(80).IsRequired();
        b.Property(x => x.DeviceId).HasMaxLength(80);
        b.Property(x => x.AnomalyType).HasMaxLength(80);
        b.Property(x => x.Severity).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => x.UserId);
    }
}

public sealed class DeviceIdentificationConfig : IEntityTypeConfiguration<DeviceIdentificationResult>
{
    public void Configure(EntityTypeBuilder<DeviceIdentificationResult> b)
    {
        b.ToTable("an_device_identifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(80).IsRequired();
        b.Property(x => x.DeviceId).HasMaxLength(80);
        b.Property(x => x.PredictedDeviceType).HasMaxLength(120);
        b.Property(x => x.Status).HasMaxLength(20);
        b.HasIndex(x => x.UserId);
    }
}

public sealed class ConsumptionRankingConfig : IEntityTypeConfiguration<ConsumptionRanking>
{
    public void Configure(EntityTypeBuilder<ConsumptionRanking> b)
    {
        b.ToTable("an_consumption_rankings");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(80).IsRequired();
        b.Property(x => x.PeriodType).HasMaxLength(40);
        b.Property(x => x.RankingsJson).HasColumnType("text");
        b.HasIndex(x => x.UserId);
    }
}

// ---------------------------------------------------------------- adaptadores

public sealed class BillPredictionRepository : IBillPredictionRepository
{
    private readonly SemsDbContext _db;
    public BillPredictionRepository(SemsDbContext db) => _db = db;

    public async Task<BillPrediction> SaveAsync(BillPrediction p, CancellationToken ct = default)
    {
        if (_db.Entry(p).State == EntityState.Detached) _db.Add(p);
        await _db.SaveChangesAsync(ct);
        return p;
    }

    public Task<List<BillPrediction>> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        _db.Set<BillPrediction>().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.GeneratedAt).ToListAsync(ct);
}

public sealed class RecommendationRepository : IRecommendationRepository
{
    private readonly SemsDbContext _db;
    public RecommendationRepository(SemsDbContext db) => _db = db;

    public async Task<Recommendation> SaveAsync(Recommendation r, CancellationToken ct = default)
    {
        if (_db.Entry(r).State == EntityState.Detached) _db.Add(r);
        await _db.SaveChangesAsync(ct);
        return r;
    }

    public Task<Recommendation?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Set<Recommendation>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<Recommendation>> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        _db.Set<Recommendation>().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.GeneratedAt).ToListAsync(ct);
}

public sealed class AnomalyRepository : IAnomalyRepository
{
    private readonly SemsDbContext _db;
    public AnomalyRepository(SemsDbContext db) => _db = db;

    public async Task<Anomaly> SaveAsync(Anomaly a, CancellationToken ct = default)
    {
        if (_db.Entry(a).State == EntityState.Detached) _db.Add(a);
        await _db.SaveChangesAsync(ct);
        return a;
    }

    public Task<Anomaly?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Set<Anomaly>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<Anomaly>> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        _db.Set<Anomaly>().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.DetectedAt).ToListAsync(ct);
}

public sealed class DeviceIdentificationRepository : IDeviceIdentificationRepository
{
    private readonly SemsDbContext _db;
    public DeviceIdentificationRepository(SemsDbContext db) => _db = db;

    public async Task<DeviceIdentificationResult> SaveAsync(DeviceIdentificationResult d,
        CancellationToken ct = default)
    {
        if (_db.Entry(d).State == EntityState.Detached) _db.Add(d);
        await _db.SaveChangesAsync(ct);
        return d;
    }

    public Task<List<DeviceIdentificationResult>> FindByUserIdAsync(string userId,
        CancellationToken ct = default) =>
        _db.Set<DeviceIdentificationResult>().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AnalyzedAt).ToListAsync(ct);
}

public sealed class ConsumptionRankingRepository : IConsumptionRankingRepository
{
    private readonly SemsDbContext _db;
    public ConsumptionRankingRepository(SemsDbContext db) => _db = db;

    public async Task<ConsumptionRanking> SaveAsync(ConsumptionRanking c, CancellationToken ct = default)
    {
        if (_db.Entry(c).State == EntityState.Detached) _db.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    public Task<List<ConsumptionRanking>> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        _db.Set<ConsumptionRanking>().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.GeneratedAt).ToListAsync(ct);
}
