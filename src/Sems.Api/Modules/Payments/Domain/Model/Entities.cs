namespace Sems.Api.Modules.Payments.Domain.Model;

/// <summary>Cobro asociado a una suscripcion.</summary>
public class Payment
{
    public Guid PaymentId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? PaymentMethodId { get; private set; }
    public double Amount { get; private set; }
    public string Currency { get; private set; } = "pen";
    public PaymentStatus Status { get; private set; }
    public string? PaymentMethod { get; private set; }
    public string? StripePaymentIntentId { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Payment()
    {
    }

    /// <summary>El importe pasa por <see cref="Money"/>, que garantiza que sea positivo.</summary>
    public static Payment Create(Guid? subscriptionId, Guid userId, Guid? paymentMethodId,
        double amount, string? currency, string? paymentMethod)
    {
        var money = new Money(amount, currency);
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            UserId = userId,
            PaymentMethodId = paymentMethodId,
            Amount = money.Amount,
            Currency = money.Currency,
            Status = PaymentStatus.pending,
            PaymentMethod = paymentMethod,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkProcessing(string? intentId)
    {
        Status = PaymentStatus.processing;
        StripePaymentIntentId = intentId;
    }

    public void MarkProcessed(string? intentId)
    {
        Status = PaymentStatus.processed;
        StripePaymentIntentId = intentId;
        PaidAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? intentId)
    {
        Status = PaymentStatus.failed;
        StripePaymentIntentId = intentId;
    }

    public void MarkCancelled(string? intentId)
    {
        Status = PaymentStatus.cancelled;
        StripePaymentIntentId = intentId;
    }

    public bool IsPaid => Status == PaymentStatus.processed;
}

/// <summary>
/// Medio de pago guardado del usuario.
///
/// <para>Solo se almacenan los datos que Stripe devuelve para mostrar la
/// tarjeta: marca, ultimos cuatro digitos y vencimiento. <b>El numero completo y
/// el CVV nunca tocan esta base de datos.</b></para>
/// </summary>
public class PaymentMethodEntity
{
    public Guid PaymentMethodId { get; private set; }
    public Guid UserId { get; private set; }
    public string? Type { get; private set; }
    public string? Brand { get; private set; }
    public string? Last4 { get; private set; }
    public int ExpMonth { get; private set; }
    public int ExpYear { get; private set; }
    public string? StripePaymentMethodId { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PaymentMethodEntity()
    {
    }

    public static PaymentMethodEntity Create(Guid userId, string? type, string? brand, string? last4,
        int expMonth, int expYear, string? stripePaymentMethodId, bool isDefault) => new()
    {
        PaymentMethodId = Guid.NewGuid(),
        UserId = userId,
        Type = type,
        Brand = brand,
        Last4 = last4,
        ExpMonth = expMonth,
        ExpYear = expYear,
        StripePaymentMethodId = stripePaymentMethodId,
        IsDefault = isDefault,
        CreatedAt = DateTime.UtcNow
    };

    public void MarkDefault() => IsDefault = true;

    public void RemoveDefault() => IsDefault = false;
}

/// <summary>Comprobante emitido cuando un cobro se completa.</summary>
public class Invoice
{
    public Guid InvoiceId { get; private set; }
    public Guid PaymentId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public double TotalAmount { get; private set; }
    public string? PdfUrl { get; private set; }

    private Invoice()
    {
    }

    /// <summary>
    /// El numero de comprobante se compone de la fecha y el primer bloque del
    /// identificador, igual que en el servicio original: <c>INV-20260905-A1B2C3D4</c>.
    /// </summary>
    public static Invoice IssueFor(Guid paymentId, double totalAmount, string? pdfUrl)
    {
        var invoiceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var shortId = invoiceId.ToString().Split('-')[0].ToUpperInvariant();

        return new Invoice
        {
            InvoiceId = invoiceId,
            PaymentId = paymentId,
            InvoiceNumber = $"INV-{now:yyyyMMdd}-{shortId}",
            IssuedAt = now,
            TotalAmount = totalAmount,
            PdfUrl = pdfUrl
        };
    }
}

/// <summary>
/// Evento recibido del proveedor de pagos.
///
/// <para>Se guarda el cuerpo original para auditoria y, sobre todo, para
/// detectar reenvios: Stripe reintenta los webhooks, asi que sin este registro
/// un mismo cobro podria contabilizarse dos veces.</para>
/// </summary>
public class PaymentWebhookEvent
{
    public const string ProviderStripe = "stripe";

    public Guid EventId { get; private set; }
    public string? Provider { get; private set; }
    public string ProviderEventId { get; private set; } = string.Empty;
    public string? EventType { get; private set; }
    public string? Payload { get; private set; }
    public bool Processed { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private PaymentWebhookEvent()
    {
    }

    public static PaymentWebhookEvent Received(string provider, string providerEventId,
        string? eventType, string? payload) => new()
    {
        EventId = Guid.NewGuid(),
        Provider = provider,
        ProviderEventId = providerEventId,
        EventType = eventType,
        Payload = payload,
        Processed = false,
        ReceivedAt = DateTime.UtcNow
    };

    public void MarkProcessed()
    {
        Processed = true;
        ProcessedAt = DateTime.UtcNow;
    }
}
