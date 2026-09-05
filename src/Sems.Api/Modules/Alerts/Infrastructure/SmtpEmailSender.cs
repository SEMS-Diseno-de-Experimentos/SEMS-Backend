using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Sems.Api.Modules.Alerts.Domain.Services;

namespace Sems.Api.Modules.Alerts.Infrastructure;

/// <summary>
/// Envio de correo por SMTP.
///
/// <para>El interruptor <c>Mail:Enabled</c> permite desactivar el envio en
/// desarrollo sin tocar codigo: el mensaje se registra en el log en lugar de
/// salir.</para>
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _from;
    private readonly bool _enabled;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;
        _host = Read(configuration, "Mail:Host", "MAIL_HOST") ?? "smtp.gmail.com";
        _port = int.TryParse(Read(configuration, "Mail:Port", "MAIL_PORT"), out var p) ? p : 587;
        _username = Read(configuration, "Mail:Username", "MAIL_USERNAME") ?? string.Empty;
        _password = Read(configuration, "Mail:Password", "MAIL_PASSWORD") ?? string.Empty;
        _from = Read(configuration, "Mail:From", "MAIL_FROM") ?? _username;
        _enabled = !string.Equals(Read(configuration, "Mail:Enabled", "MAIL_ENABLED"), "false",
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Correo desactivado; no se envia a {To} con asunto '{Subject}'",
                to, subject);
            return;
        }
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentException("destinatario vacio", nameof(to));
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(string.IsNullOrWhiteSpace(_from) ? _username : _from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_username, _password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Correo enviado a {To} con asunto '{Subject}'", to, subject);
    }

    private static string? Read(IConfiguration configuration, string key, string envKey) =>
        configuration[key] ?? Environment.GetEnvironmentVariable(envKey);
}
