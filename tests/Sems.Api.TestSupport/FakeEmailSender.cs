using System.Collections.Concurrent;
using Sems.Api.Modules.Alerts.Domain.Services;

namespace Sems.Api.TestSupport;

/// <summary>Correo que la aplicacion intento enviar.</summary>
public sealed record SentEmail(string To, string Subject, string Body);

/// <summary>
/// Sustituto del envio de correo: en lugar de salir por SMTP, el mensaje queda
/// guardado para que la prueba compruebe a quien se envio y que decia.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent.ToArray();

    /// <summary>Correos enviados a un destinatario.</summary>
    public IReadOnlyList<SentEmail> SentTo(string recipient) => Sent
        .Where(e => string.Equals(e.To, recipient, StringComparison.OrdinalIgnoreCase))
        .ToList();

    /// <summary>Correos enviados a un destinatario con un asunto concreto.</summary>
    public IReadOnlyList<SentEmail> SentTo(string recipient, string subject) => SentTo(recipient)
        .Where(e => string.Equals(e.Subject, subject, StringComparison.Ordinal))
        .ToList();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _sent.Enqueue(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }
}
