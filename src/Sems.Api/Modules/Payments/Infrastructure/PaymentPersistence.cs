using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sems.Api.Modules.Payments.Domain.Model;
using Sems.Api.Modules.Payments.Domain.Repositories;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Payments.Infrastructure;

public sealed class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("pm_payments");
        b.HasKey(p => p.PaymentId);
        b.Property(p => p.Currency).HasMaxLength(10);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.PaymentMethod).HasMaxLength(60);
        b.Property(p => p.StripePaymentIntentId).HasMaxLength(120);
        b.HasIndex(p => p.UserId);
        b.HasIndex(p => p.SubscriptionId);
        b.HasIndex(p => p.StripePaymentIntentId);
    }
}

public sealed class PaymentMethodConfig : IEntityTypeConfiguration<PaymentMethodEntity>
{
    public void Configure(EntityTypeBuilder<PaymentMethodEntity> b)
    {
        b.ToTable("pm_payment_methods");
        b.HasKey(m => m.PaymentMethodId);
        b.Property(m => m.Type).HasMaxLength(40);
        b.Property(m => m.Brand).HasMaxLength(40);
        b.Property(m => m.Last4).HasMaxLength(8);
        b.Property(m => m.StripePaymentMethodId).HasMaxLength(120);
        b.HasIndex(m => m.UserId);
    }
}

public sealed class InvoiceConfig : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("pm_invoices");
        b.HasKey(i => i.InvoiceId);
        b.Property(i => i.InvoiceNumber).HasMaxLength(60);
        b.Property(i => i.PdfUrl).HasMaxLength(500);
        b.HasIndex(i => i.PaymentId);
    }
}

/// <summary>
/// Evento de webhook.
///
/// <para>El identificador del proveedor es unico: es lo que impide procesar dos
/// veces el mismo cobro cuando Stripe reintenta la entrega.</para>
/// </summary>
public sealed class WebhookEventConfig : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> b)
    {
        b.ToTable("pm_webhook_events");
        b.HasKey(e => e.EventId);
        b.Property(e => e.Provider).HasMaxLength(40);
        b.Property(e => e.ProviderEventId).HasMaxLength(160).IsRequired();
        b.Property(e => e.EventType).HasMaxLength(120);
        b.Property(e => e.Payload).HasColumnType("text");
        b.HasIndex(e => e.ProviderEventId).IsUnique();
    }
}

// ---------------------------------------------------------------- adaptadores

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly SemsDbContext _db;
    public PaymentRepository(SemsDbContext db) => _db = db;

    public async Task<Payment> SaveAsync(Payment p, CancellationToken ct = default)
    {
        if (_db.Entry(p).State == EntityState.Detached) _db.Add(p);
        await _db.SaveChangesAsync(ct);
        return p;
    }

    public Task<Payment?> FindByIdAsync(Guid paymentId, CancellationToken ct = default) =>
        _db.Set<Payment>().FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);

    public Task<List<Payment>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<Payment>().Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public Task<List<Payment>> FindBySubscriptionIdAsync(Guid subscriptionId, CancellationToken ct = default) =>
        _db.Set<Payment>().Where(p => p.SubscriptionId == subscriptionId)
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public Task<Payment?> FindByStripePaymentIntentIdAsync(string? intentId, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(intentId)
            ? Task.FromResult<Payment?>(null)
            : _db.Set<Payment>().FirstOrDefaultAsync(p => p.StripePaymentIntentId == intentId, ct);
}

public sealed class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly SemsDbContext _db;
    public PaymentMethodRepository(SemsDbContext db) => _db = db;

    public async Task<PaymentMethodEntity> SaveAsync(PaymentMethodEntity m, CancellationToken ct = default)
    {
        if (_db.Entry(m).State == EntityState.Detached) _db.Add(m);
        await _db.SaveChangesAsync(ct);
        return m;
    }

    public Task<PaymentMethodEntity?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Set<PaymentMethodEntity>().FirstOrDefaultAsync(m => m.PaymentMethodId == id, ct);

    public Task<List<PaymentMethodEntity>> FindByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<PaymentMethodEntity>().Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt).ToListAsync(ct);

    public Task<PaymentMethodEntity?> FindDefaultByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<PaymentMethodEntity>()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsDefault, ct);

    public async Task DeleteAsync(Guid paymentMethodId, CancellationToken ct = default)
    {
        var method = await FindByIdAsync(paymentMethodId, ct);
        if (method is not null)
        {
            _db.Remove(method);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly SemsDbContext _db;
    public InvoiceRepository(SemsDbContext db) => _db = db;

    public async Task<Invoice> SaveAsync(Invoice i, CancellationToken ct = default)
    {
        if (_db.Entry(i).State == EntityState.Detached) _db.Add(i);
        await _db.SaveChangesAsync(ct);
        return i;
    }

    public Task<Invoice?> FindByIdAsync(Guid invoiceId, CancellationToken ct = default) =>
        _db.Set<Invoice>().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

    public Task<Invoice?> FindByPaymentIdAsync(Guid paymentId, CancellationToken ct = default) =>
        _db.Set<Invoice>().FirstOrDefaultAsync(i => i.PaymentId == paymentId, ct);
}

public sealed class WebhookEventRepository : IWebhookEventRepository
{
    private readonly SemsDbContext _db;
    public WebhookEventRepository(SemsDbContext db) => _db = db;

    public async Task<PaymentWebhookEvent> SaveAsync(PaymentWebhookEvent e, CancellationToken ct = default)
    {
        if (_db.Entry(e).State == EntityState.Detached) _db.Add(e);
        await _db.SaveChangesAsync(ct);
        return e;
    }

    public Task<PaymentWebhookEvent?> FindByProviderEventIdAsync(string providerEventId,
        CancellationToken ct = default) =>
        _db.Set<PaymentWebhookEvent>()
            .FirstOrDefaultAsync(e => e.ProviderEventId == providerEventId, ct);
}
