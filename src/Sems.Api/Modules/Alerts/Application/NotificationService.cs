using Polly;
using Polly.Retry;
using Sems.Api.Modules.Alerts.Domain.Model;
using Sems.Api.Modules.Alerts.Domain.Repositories;
using Sems.Api.Modules.Alerts.Domain.Services;

namespace Sems.Api.Modules.Alerts.Application;

/// <summary>
/// Envio de correo con reintentos y registro.
///
/// <para>Reemplaza al <c>sendWithRetry</c> escrito a mano en Go: aqui los tres
/// intentos con espera creciente los aporta Polly, y solo cuando se agotan se
/// deja el fallo asentado en la bitacora.</para>
///
/// <para>Un correo que no sale <b>nunca</b> debe tumbar la operacion de negocio
/// que lo origino: el cobro ya ocurrio aunque el comprobante no llegue. Por eso
/// el fallo definitivo se registra y se calla.</para>
/// </summary>
public sealed class NotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly INotificationLogRepository _logs;
    private readonly ILogger<NotificationService> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public NotificationService(IEmailSender emailSender, INotificationLogRepository logs,
        ILogger<NotificationService> logger)
    {
        _emailSender = emailSender;
        _logs = logs;
        _logger = logger;

        // Tres intentos con espera creciente: 2s, 4s, 8s.
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning("Reintentando envio de correo (intento {Attempt})",
                        args.AttemptNumber + 1);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task SendEmailAsync(Guid? alertId, string recipient, string subject, string body,
        CancellationToken ct = default)
    {
        try
        {
            await _retryPipeline.ExecuteAsync(
                async token => await _emailSender.SendAsync(recipient, subject, body, token), ct);

            await _logs.SaveAsync(NotificationLog.Sent(alertId, "email", recipient), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el correo a {Recipient} tras 3 intentos",
                recipient);
            await _logs.SaveAsync(
                NotificationLog.Failed(alertId, "email", recipient, ex.Message), ct);
        }
    }
}
