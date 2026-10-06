using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sems.Api.Modules.Subscriptions.Domain.Model;
using Sems.Api.Modules.Subscriptions.Domain.Repositories;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Subscriptions.Infrastructure;

public sealed class PlanConfig : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("sb_subscription_plans");
        b.HasKey(p => p.PlanId);
        b.Property(p => p.Name).HasMaxLength(80).IsRequired();
        b.Property(p => p.Currency).HasMaxLength(10);
        b.Property(p => p.BillingPeriod).HasMaxLength(40);
        b.HasIndex(p => p.Name).IsUnique();

        // Las caracteristicas se cargan y se guardan con el plan: es un
        // agregado, no dos entidades independientes.
        b.HasMany(p => p.PlanFeatures)
            .WithOne()
            .HasForeignKey(f => f.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.PlanFeatures).AutoInclude();
    }
}

public sealed class PlanFeatureConfig : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> b)
    {
        b.ToTable("sb_plan_features");
        b.HasKey(f => f.FeatureId);
        b.Property(f => f.FeatureCode).HasMaxLength(80);
        b.Property(f => f.FeatureName).HasMaxLength(200);
        b.Property(f => f.FeatureValue).HasMaxLength(200);
        b.HasIndex(f => f.PlanId);
    }
}

public sealed class SubscriptionConfig : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("sb_subscriptions");
        b.HasKey(s => s.SubscriptionId);
        b.Property(s => s.UserId).HasMaxLength(80).IsRequired();
        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(s => s.StripeSubscriptionId).HasMaxLength(120);
        b.HasIndex(s => s.UserId);
        b.HasIndex(s => s.StripeSubscriptionId);
    }
}

// ---------------------------------------------------------------- adaptadores

public sealed class PlanRepository : IPlanRepository
{
    private readonly SemsDbContext _db;
    public PlanRepository(SemsDbContext db) => _db = db;

    public async Task<SubscriptionPlan> SaveAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        if (_db.Entry(plan).State == EntityState.Detached) _db.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan;
    }

    public Task<SubscriptionPlan?> FindByIdAsync(Guid planId, CancellationToken ct = default) =>
        _db.Set<SubscriptionPlan>().FirstOrDefaultAsync(p => p.PlanId == planId, ct);

    public Task<SubscriptionPlan?> FindByNameAsync(string name, CancellationToken ct = default) =>
        _db.Set<SubscriptionPlan>().FirstOrDefaultAsync(p => p.Name == name, ct);

    public Task<List<SubscriptionPlan>> FindAllActiveAsync(CancellationToken ct = default) =>
        _db.Set<SubscriptionPlan>().Where(p => p.Active).OrderBy(p => p.Price).ToListAsync(ct);

    public async Task<long> CountAsync(CancellationToken ct = default) =>
        await _db.Set<SubscriptionPlan>().LongCountAsync(ct);
}

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly SemsDbContext _db;
    public SubscriptionRepository(SemsDbContext db) => _db = db;

    public async Task<Subscription> SaveAsync(Subscription s, CancellationToken ct = default)
    {
        if (_db.Entry(s).State == EntityState.Detached) _db.Add(s);
        await _db.SaveChangesAsync(ct);
        return s;
    }

    public Task<Subscription?> FindByIdAsync(Guid subscriptionId, CancellationToken ct = default) =>
        _db.Set<Subscription>().FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId, ct);

    public Task<List<Subscription>> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        _db.Set<Subscription>().Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt).ToListAsync(ct);

    public Task<Subscription?> FindByStripeSubscriptionIdAsync(string stripeSubscriptionId,
        CancellationToken ct = default) =>
        _db.Set<Subscription>()
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct);
}
