using Sems.Api.Modules.Payments.Domain.Model;
using Sems.Api.Modules.Payments.Domain.Repositories;
using Sems.Api.Modules.Payments.Domain.Services;
using Sems.Api.Shared.Errors;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Payments.Application;

/// <summary>Resultado de procesar un cobro: el pago y, si prospero, su comprobante.</summary>
public sealed record PaymentResult(Payment Payment, Invoice? Invoice);

/// <summary>Casos de uso de cobro.</summary>
public sealed class PaymentCommandService
{
    /// <summary>Marca del metodo cuando el cobro viene de una sesion de Stripe Checkout.</summary>
    private const string MethodCheckout = "stripe_checkout";

    private readonly IPaymentRepository _payments;
    private readonly IPaymentMethodRepository _methods;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentProvider _provider;
    private readonly PaymentStatusMapper _statusMapper;
    private readonly IDomainEventBus _bus;
    private readonly ILogger<PaymentCommandService> _logger;

    public PaymentCommandService(IPaymentRepository payments, IPaymentMethodRepository methods,
        IInvoiceRepository invoices, IPaymentProvider provider, PaymentStatusMapper statusMapper,
        IDomainEventBus bus, ILogger<PaymentCommandService> logger)
    {
        _payments = payments;
        _methods = methods;
        _invoices = invoices;
        _provider = provider;
        _statusMapper = statusMapper;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Cobra con una tarjeta ya guardada.
    ///
    /// <para>El pago se guarda antes de llamar a Stripe: si la llamada externa
    /// falla o el proceso muere a mitad, queda constancia del intento en lugar
    /// de un cobro fantasma sin registro.</para>
    /// </summary>
    public async Task<PaymentResult> ProcessAsync(Guid? subscriptionId, Guid userId,
        Guid paymentMethodId, double amount, string? currency, string? paymentMethodLabel,
        CancellationToken ct = default)
    {
        var method = await _methods.FindByIdAsync(paymentMethodId, ct)
                     ?? throw AppException.NotFound("payment method not found");

        if (method.UserId != userId)
        {
            throw AppException.Unauthorized("unauthorized resource");
        }

        var payment = await _payments.SaveAsync(Payment.Create(subscriptionId, userId,
            paymentMethodId, amount, currency, paymentMethodLabel), ct);

        var result = await _provider.CreatePaymentIntentAsync(new CreatePaymentIntentRequest(
            amount, currency, method.StripePaymentMethodId, userId.ToString(),
            subscriptionId?.ToString(), payment.PaymentId.ToString()), ct);

        switch (_statusMapper.FromStripe(result.Status))
        {
            case PaymentStatus.processed: payment.MarkProcessed(result.Id); break;
            case PaymentStatus.failed: payment.MarkFailed(result.Id); break;
            case PaymentStatus.cancelled: payment.MarkCancelled(result.Id); break;
            default: payment.MarkProcessing(result.Id); break;
        }

        Invoice? invoice = null;
        if (payment.IsPaid)
        {
            PublishProcessed(payment);
            invoice = await _invoices.SaveAsync(
                Invoice.IssueFor(payment.PaymentId, payment.Amount, null), ct);
        }

        var saved = await _payments.SaveAsync(payment, ct);
        return new PaymentResult(saved, invoice);
    }

    /// <summary>
    /// Crea una sesion de Stripe Checkout.
    ///
    /// <para>El usuario paga en la pagina de Stripe y vuelve a la aplicacion. El
    /// registro del cobro no se crea aqui: se crea cuando llega el webhook
    /// <c>checkout.session.completed</c>, que es el unico aviso fiable de que el
    /// dinero se movio.</para>
    /// </summary>
    public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid userId,
        Guid? subscriptionId, string? planName, double amount, string? currency,
        string? successUrl, string? cancelUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(cancelUrl))
        {
            throw AppException.Validation("success_url and cancel_url are required");
        }

        return _provider.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest(
            userId.ToString(), subscriptionId?.ToString(), planName, amount, currency,
            successUrl, cancelUrl), ct);
    }

    /// <summary>
    /// Registra el cobro que confirma una sesion de Checkout.
    ///
    /// <para>Es idempotente a proposito: Stripe reintenta los webhooks, y sin
    /// esta comprobacion un mismo pago se registraria varias veces.</para>
    /// </summary>
    public async Task<Payment> RecordCheckoutPaymentAsync(Guid userId, Guid? subscriptionId,
        double amount, string? currency, string? paymentIntentId, CancellationToken ct = default)
    {
        var existing = await _payments.FindByStripePaymentIntentIdAsync(paymentIntentId, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Cobro de checkout ya registrado para el intent {Intent}",
                paymentIntentId);
            return existing;
        }

        var payment = Payment.Create(subscriptionId, userId, null, amount, currency, MethodCheckout);
        payment.MarkProcessed(paymentIntentId);

        PublishProcessed(payment);

        var saved = await _payments.SaveAsync(payment, ct);
        await _invoices.SaveAsync(Invoice.IssueFor(saved.PaymentId, saved.Amount, null), ct);
        return saved;
    }

    /// <summary>
    /// Publica el cobro completado.
    ///
    /// <para>Lo escucha el modulo de notificaciones para enviar el comprobante
    /// por correo. Antes viajaba por el topic <c>payments.events</c>.</para>
    /// </summary>
    private void PublishProcessed(Payment payment) =>
        _bus.Publish(new DomainEvents.PaymentProcessed(payment.UserId, payment.PaymentId,
            (decimal)payment.Amount, payment.Currency, payment.Status.ToString()));
}

/// <summary>Alta y gestion de medios de pago del usuario.</summary>
public sealed class PaymentMethodCommandService
{
    private readonly IPaymentMethodRepository _methods;
    private readonly IPaymentProvider _provider;

    public PaymentMethodCommandService(IPaymentMethodRepository methods, IPaymentProvider provider)
    {
        _methods = methods;
        _provider = provider;
    }

    /// <summary>
    /// Guarda una tarjeta.
    ///
    /// <para>Los datos visibles se leen de Stripe, no del cliente: asi la marca y
    /// los ultimos digitos que mostramos son los reales y no lo que alguien
    /// decida enviar en el cuerpo de la peticion.</para>
    /// </summary>
    public async Task<PaymentMethodEntity> RegisterAsync(Guid userId, string? type,
        string stripePaymentMethodId, bool isDefault, CancellationToken ct = default)
    {
        var details = await _provider.GetPaymentMethodDetailsAsync(stripePaymentMethodId, ct);

        var existing = await _methods.FindByUserIdAsync(userId, ct);
        var shouldBeDefault = isDefault || existing.Count == 0;

        if (shouldBeDefault)
        {
            await ClearCurrentDefaultAsync(userId, ct);
        }

        return await _methods.SaveAsync(PaymentMethodEntity.Create(userId,
            string.IsNullOrWhiteSpace(type) ? details.Type : type,
            details.Brand, details.Last4, details.ExpMonth, details.ExpYear,
            stripePaymentMethodId, shouldBeDefault), ct);
    }

    public async Task<PaymentMethodEntity> SetDefaultAsync(Guid paymentMethodId,
        CancellationToken ct = default)
    {
        var method = await _methods.FindByIdAsync(paymentMethodId, ct)
                     ?? throw AppException.NotFound("payment method not found");

        await ClearCurrentDefaultAsync(method.UserId, ct);
        method.MarkDefault();
        return await _methods.SaveAsync(method, ct);
    }

    public async Task DeleteAsync(Guid paymentMethodId, CancellationToken ct = default)
    {
        _ = await _methods.FindByIdAsync(paymentMethodId, ct)
            ?? throw AppException.NotFound("payment method not found");
        await _methods.DeleteAsync(paymentMethodId, ct);
    }

    /// <summary>Solo puede haber un medio de pago predeterminado por usuario.</summary>
    private async Task ClearCurrentDefaultAsync(Guid userId, CancellationToken ct)
    {
        var current = await _methods.FindDefaultByUserIdAsync(userId, ct);
        if (current is not null)
        {
            current.RemoveDefault();
            await _methods.SaveAsync(current, ct);
        }
    }
}

/// <summary>Consultas del modulo de pagos.</summary>
public sealed class PaymentQueryService
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentMethodRepository _methods;
    private readonly IInvoiceRepository _invoices;

    public PaymentQueryService(IPaymentRepository payments, IPaymentMethodRepository methods,
        IInvoiceRepository invoices)
    {
        _payments = payments;
        _methods = methods;
        _invoices = invoices;
    }

    public async Task<Payment> PaymentByIdAsync(Guid paymentId, CancellationToken ct = default) =>
        await _payments.FindByIdAsync(paymentId, ct) ?? throw AppException.NotFound("payment not found");

    public Task<List<Payment>> PaymentsByUserAsync(Guid userId, CancellationToken ct = default) =>
        _payments.FindByUserIdAsync(userId, ct);

    public Task<List<Payment>> PaymentsBySubscriptionAsync(Guid subscriptionId,
        CancellationToken ct = default) => _payments.FindBySubscriptionIdAsync(subscriptionId, ct);

    public Task<List<PaymentMethodEntity>> MethodsByUserAsync(Guid userId,
        CancellationToken ct = default) => _methods.FindByUserIdAsync(userId, ct);

    public async Task<Invoice> InvoiceByIdAsync(Guid invoiceId, CancellationToken ct = default) =>
        await _invoices.FindByIdAsync(invoiceId, ct) ?? throw AppException.NotFound("invoice not found");

    public async Task<Invoice> InvoiceByPaymentAsync(Guid paymentId, CancellationToken ct = default) =>
        await _invoices.FindByPaymentIdAsync(paymentId, ct)
        ?? throw AppException.NotFound("invoice not found");
}
