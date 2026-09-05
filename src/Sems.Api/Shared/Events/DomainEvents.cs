namespace Sems.Api.Shared.Events;

/// <summary>
/// Contrato comun de todo evento de dominio: sabe cuando ocurrio y a que
/// usuario pertenece.
/// </summary>
public interface IDomainEvent
{
    Guid UserId { get; }

    DateTime OccurredAt { get; }
}

/// <summary>
/// Eventos que cruzan bounded contexts.
///
/// <para>Sustituyen a los topics de Kafka del disenio anterior. Cada topic paso
/// a ser un tipo de evento; la entrega ocurre dentro del mismo proceso, despues
/// de que la transaccion del emisor confirme.</para>
///
/// <para>Equivalencia con el disenio de microservicios:</para>
/// <code>
///   iam.events            -> UserRegistered, UserLoggedIn, VerificationRequested, PasswordResetRequested
///   device.events         -> DeviceRegistered, DeviceLinked, DeviceUnlinked, DeviceStatusUpdated
///   energy.events         -> ReadingProcessed
///   alerts.events         -> AlertTriggered
///   payments.events       -> PaymentProcessed
///   subscriptions.events  -> SubscriptionChanged
/// </code>
///
/// <para>Los consumidores no conocen al emisor: escuchan el tipo. Si en el
/// futuro se vuelve a un broker, basta anadir un adaptador que reenvie estos
/// mismos eventos, sin tocar emisores ni consumidores.</para>
/// </summary>
public static class DomainEvents
{
    // ------------------------------------------------------------------ iam

    public sealed record UserRegistered(Guid UserId, string EmailAddress, string Role)
        : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    public sealed record UserLoggedIn(Guid UserId, string EmailAddress) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    public sealed record RoleAssigned(Guid UserId, string Role) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>Pide al modulo de notificaciones el codigo de verificacion.</summary>
    public sealed record VerificationRequested(Guid UserId, string EmailAddress, string Token)
        : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>Pide el enlace de recuperacion de contrasena.</summary>
    public sealed record PasswordResetRequested(Guid UserId, string EmailAddress, string Token)
        : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    // -------------------------------------------------------------- devices

    public sealed record DeviceRegistered(Guid UserId, Guid DeviceId, string Name, string Type)
        : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    public sealed record DeviceLinked(Guid UserId, Guid DeviceId, Guid BindingId) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    public sealed record DeviceUnlinked(Guid UserId, Guid DeviceId, Guid BindingId) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    public sealed record DeviceStatusUpdated(Guid UserId, Guid DeviceId, string Status)
        : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    // --------------------------------------------------------------- energy

    /// <summary>
    /// Lectura del medidor ya validada y almacenada. La escuchan analytics
    /// (rankings y proyecciones) y alerts (evaluacion de umbrales).
    /// </summary>
    public sealed record ReadingProcessed(Guid UserId, Guid? DeviceId, Guid? MeterId,
        decimal ConsumptionKwh, DateTime RecordedAt) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    // --------------------------------------------------------------- alerts

    public sealed record AlertTriggered(Guid UserId, Guid AlertId, string AlertType,
        string Severity, string Message) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    // ------------------------------------------------------------- payments

    public sealed record PaymentProcessed(Guid UserId, Guid PaymentId, decimal Amount,
        string Currency, string Status) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    // -------------------------------------------------------- subscriptions

    public sealed record SubscriptionChanged(Guid UserId, Guid SubscriptionId, string PlanName,
        string Status) : IDomainEvent
    {
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }
}
