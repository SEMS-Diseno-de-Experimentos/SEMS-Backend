using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Modules.Alerts.Domain.Repositories;
using Sems.Api.Modules.Iam.Interfaces.Acl;
using Sems.Api.Shared.Events;

namespace Sems.Api.Modules.Alerts.Application;

/// <summary>
/// Convierte eventos de dominio en correos al usuario.
///
/// <para>Sustituye al consumidor de Kafka del servicio de alertas. Cada clase
/// escucha un tipo de evento en lugar de un topic, y el bus solo las invoca
/// despues de que la transaccion que origino el evento haya confirmado.</para>
/// </summary>
public sealed class UserRegisteredHandler : IDomainEventHandler<DomainEvents.UserRegistered>
{
    private readonly NotificationService _notifications;
    private readonly string _appBaseUrl;

    public UserRegisteredHandler(NotificationService notifications, IConfiguration configuration)
    {
        _notifications = notifications;
        _appBaseUrl = AppUrl.From(configuration);
    }

    public Task HandleAsync(DomainEvents.UserRegistered e, CancellationToken ct = default) =>
        _notifications.SendEmailAsync(null, e.EmailAddress, "Bienvenido a SEMS",
            $"""
             Hola:

             Tu cuenta en SEMS ya esta creada. Desde ahora puedes vincular tu
             medidor EOS y empezar a ver en que se va tu recibo de luz.

             Entra aqui: {_appBaseUrl}

             El equipo de SEMS
             """, ct);
}

/// <summary>
/// Codigo de verificacion de cuenta.
///
/// <para>El token llega en el evento y no se vuelve a consultar: quien lo emitio
/// es el unico que lo conoce en claro, ya que en base de datos se guarda su
/// resumen.</para>
/// </summary>
public sealed class VerificationRequestedHandler
    : IDomainEventHandler<DomainEvents.VerificationRequested>
{
    private readonly NotificationService _notifications;
    private readonly string _appBaseUrl;

    public VerificationRequestedHandler(NotificationService notifications,
        IConfiguration configuration)
    {
        _notifications = notifications;
        _appBaseUrl = AppUrl.From(configuration);
    }

    public Task HandleAsync(DomainEvents.VerificationRequested e, CancellationToken ct = default) =>
        _notifications.SendEmailAsync(null, e.EmailAddress, "Verifica tu cuenta de SEMS",
            $"""
             Hola:

             Para activar tu cuenta usa este codigo:

                 {e.Token}

             O entra directamente aqui: {_appBaseUrl}/verify?token={e.Token}

             Si no fuiste tu, ignora este mensaje.
             """, ct);
}

public sealed class PasswordResetRequestedHandler
    : IDomainEventHandler<DomainEvents.PasswordResetRequested>
{
    private readonly NotificationService _notifications;
    private readonly string _appBaseUrl;

    public PasswordResetRequestedHandler(NotificationService notifications,
        IConfiguration configuration)
    {
        _notifications = notifications;
        _appBaseUrl = AppUrl.From(configuration);
    }

    public Task HandleAsync(DomainEvents.PasswordResetRequested e, CancellationToken ct = default) =>
        _notifications.SendEmailAsync(null, e.EmailAddress, "Recupera tu contrasena de SEMS",
            $"""
             Hola:

             Recibimos una solicitud para cambiar tu contrasena. Usa este enlace:

                 {_appBaseUrl}/reset-password?token={e.Token}

             El enlace caduca en una hora. Si no fuiste tu, ignora este mensaje
             y tu contrasena seguira igual.
             """, ct);
}

/// <summary>Comprobante de pago. El correo se resuelve preguntando a IAM.</summary>
public sealed class PaymentProcessedHandler : IDomainEventHandler<DomainEvents.PaymentProcessed>
{
    private readonly NotificationService _notifications;
    private readonly IIamAcl _iam;
    private readonly ILogger<PaymentProcessedHandler> _logger;
    private readonly string _appBaseUrl;

    public PaymentProcessedHandler(NotificationService notifications, IIamAcl iam,
        IConfiguration configuration, ILogger<PaymentProcessedHandler> logger)
    {
        _notifications = notifications;
        _iam = iam;
        _logger = logger;
        _appBaseUrl = AppUrl.From(configuration);
    }

    public async Task HandleAsync(DomainEvents.PaymentProcessed e, CancellationToken ct = default)
    {
        var email = await _iam.EmailOfAsync(e.UserId, ct);
        if (email is null)
        {
            _logger.LogWarning("No se encontro correo para el usuario {UserId}", e.UserId);
            return;
        }

        await _notifications.SendEmailAsync(null, email, "Comprobante de pago SEMS",
            $"""
             Hola:

             Registramos tu pago correctamente.

                 Importe:      {e.Amount} {e.Currency.ToUpperInvariant()}
                 Referencia:   {e.PaymentId}
                 Estado:       {e.Status}

             Puedes ver el detalle en {_appBaseUrl}/subscription

             Gracias por usar SEMS.
             """, ct);
    }
}

/// <summary>Alerta de consumo disparada por un umbral o una regla de inactividad.</summary>
public sealed class AlertTriggeredHandler : IDomainEventHandler<DomainEvents.AlertTriggered>
{
    private readonly NotificationService _notifications;
    private readonly IIamAcl _iam;
    private readonly ILogger<AlertTriggeredHandler> _logger;
    private readonly string _appBaseUrl;

    public AlertTriggeredHandler(NotificationService notifications, IIamAcl iam,
        IConfiguration configuration, ILogger<AlertTriggeredHandler> logger)
    {
        _notifications = notifications;
        _iam = iam;
        _logger = logger;
        _appBaseUrl = AppUrl.From(configuration);
    }

    public async Task HandleAsync(DomainEvents.AlertTriggered e, CancellationToken ct = default)
    {
        var email = await _iam.EmailOfAsync(e.UserId, ct);
        if (email is null)
        {
            _logger.LogWarning("No se encontro correo para el usuario {UserId}", e.UserId);
            return;
        }

        await _notifications.SendEmailAsync(e.AlertId, email, "Alerta de consumo en tu local",
            $"""
             Hola:

             {e.Message}

             Severidad: {e.Severity}

             Revisa el detalle en {_appBaseUrl}/alerts
             """, ct);
    }
}

/// <summary>
/// Evalua los umbrales cada vez que llega una lectura.
///
/// <para>Sustituye al consumidor del topic <c>energy.events</c>. Si alguna regla
/// activa del dispositivo se rompe, levanta la alerta correspondiente.</para>
/// </summary>
public sealed class ReadingProcessedHandler : IDomainEventHandler<DomainEvents.ReadingProcessed>
{
    private readonly IThresholdRepository _thresholds;
    private readonly AlertCommandService _alertCommands;
    private readonly ILogger<ReadingProcessedHandler> _logger;

    public ReadingProcessedHandler(IThresholdRepository thresholds,
        AlertCommandService alertCommands, ILogger<ReadingProcessedHandler> logger)
    {
        _thresholds = thresholds;
        _alertCommands = alertCommands;
        _logger = logger;
    }

    public async Task HandleAsync(DomainEvents.ReadingProcessed e, CancellationToken ct = default)
    {
        if (e.DeviceId is null)
        {
            return;
        }

        var value = (double)e.ConsumptionKwh;

        foreach (var threshold in await _thresholds.FindActiveByDeviceIdAsync(e.DeviceId, ct))
        {
            if (!threshold.IsBreachedBy(value))
            {
                continue;
            }

            var message = $"El dispositivo supero el umbral '{threshold.ThresholdName}': " +
                          $"{value} {threshold.Operator.Symbol()} {threshold.ThresholdValue} " +
                          $"{threshold.Metric}";

            await _alertCommands.CreateAlertAsync(e.UserId, e.DeviceId, threshold.ThresholdId,
                null, "threshold_exceeded", "Consumo por encima del umbral", message, "high",
                null, e.RecordedAt, ct);

            _logger.LogInformation("Umbral {ThresholdId} roto por el dispositivo {DeviceId}",
                threshold.ThresholdId, e.DeviceId);
        }
    }
}

/// <summary>URL publica del frontend, usada en los enlaces de los correos.</summary>
internal static class AppUrl
{
    public static string From(IConfiguration configuration) =>
        configuration["App:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL")
        ?? "https://sems-web-application.vercel.app";
}
