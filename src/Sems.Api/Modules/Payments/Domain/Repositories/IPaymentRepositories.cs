using Sems.Api.Modules.Payments.Domain.Model;

namespace Sems.Api.Modules.Payments.Domain.Repositories;

/// <summary>Puertos de salida del modulo de pagos.</summary>
public interface IPaymentRepository
{
    Task<Payment> SaveAsync(Payment payment, CancellationToken ct = default);
    Task<Payment?> FindByIdAsync(Guid paymentId, CancellationToken ct = default);
    Task<List<Payment>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<Payment>> FindBySubscriptionIdAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<Payment?> FindByStripePaymentIntentIdAsync(string? intentId, CancellationToken ct = default);
}

public interface IPaymentMethodRepository
{
    Task<PaymentMethodEntity> SaveAsync(PaymentMethodEntity method, CancellationToken ct = default);
    Task<PaymentMethodEntity?> FindByIdAsync(Guid paymentMethodId, CancellationToken ct = default);
    Task<List<PaymentMethodEntity>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<PaymentMethodEntity?> FindDefaultByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid paymentMethodId, CancellationToken ct = default);
}

public interface IInvoiceRepository
{
    Task<Invoice> SaveAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> FindByIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<Invoice?> FindByPaymentIdAsync(Guid paymentId, CancellationToken ct = default);
}

public interface IWebhookEventRepository
{
    Task<PaymentWebhookEvent> SaveAsync(PaymentWebhookEvent evt, CancellationToken ct = default);
    /// <summary>Sirve para descartar reenvios del mismo evento.</summary>
    Task<PaymentWebhookEvent?> FindByProviderEventIdAsync(string providerEventId, CancellationToken ct = default);
}
