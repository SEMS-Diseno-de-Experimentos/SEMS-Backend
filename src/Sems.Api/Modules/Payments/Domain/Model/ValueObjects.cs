using System.Text.Json.Serialization;
using Sems.Api.Shared.Errors;

namespace Sems.Api.Modules.Payments.Domain.Model;

/// <summary>
/// Importe con su moneda.
///
/// <para>Mantenerlos juntos evita el error clasico de pasear un numero suelto
/// sin saber si son soles, dolares o euros. La moneda se normaliza a minusculas
/// porque es lo que espera Stripe.</para>
/// </summary>
public sealed record Money
{
    public double Amount { get; }

    public string Currency { get; }

    public Money(double amount, string? currency)
    {
        if (amount <= 0)
        {
            throw AppException.Validation("invalid amount");
        }

        var normalized = currency?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw AppException.Validation("invalid currency");
        }

        Amount = amount;
        Currency = normalized;
    }

    /// <summary>Stripe cobra en la unidad minima: centimos, no soles.</summary>
    public long ToMinorUnits() => (long)Math.Round(Amount * 100);
}

/// <summary>
/// Ciclo de vida de un pago.
///
/// <para>El camino normal es pending, processing, processed. Fallido y cancelado
/// son estados finales. Se serializan en minusculas, igual que en el servicio en
/// Go.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))]
public enum PaymentStatus
{
    pending,
    processing,
    processed,
    failed,
    cancelled
}
