namespace Sems.Api.Modules.Alerts.Domain.Services;

/// <summary>
/// Puerto de envio de correo.
///
/// <para>El dominio no sabe si detras hay Gmail, un servicio transaccional o un
/// doble de pruebas. Eso permite probar el flujo de notificaciones sin enviar
/// correos de verdad.</para>
/// </summary>
public interface IEmailSender
{
    /// <exception cref="Exception">si el envio falla; quien llama decide si reintenta</exception>
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
