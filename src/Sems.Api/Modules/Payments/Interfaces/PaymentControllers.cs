using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Payments.Application;
using static Sems.Api.Modules.Payments.Interfaces.PaymentResources;

namespace Sems.Api.Modules.Payments.Interfaces;

/// <summary>Cobros y sesiones de pago.</summary>
[ApiController]
[Route("api/v1/payments")]
[Tags("Payments")]
public sealed class PaymentController : ControllerBase
{
    private readonly PaymentCommandService _commands;
    private readonly PaymentQueryService _queries;

    public PaymentController(PaymentCommandService commands, PaymentQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Cobra con una tarjeta guardada.</summary>
    [HttpPost("process")]
    public async Task<ActionResult<ProcessPaymentResponse>> Process(
        [FromBody] ProcessPaymentRequest request)
    {
        var result = await _commands.ProcessAsync(Guid.Parse(request.SubscriptionId),
            Guid.Parse(request.UserId), Guid.Parse(request.PaymentMethodId), request.Amount,
            request.Currency, request.PaymentMethod);

        var body = new ProcessPaymentResponse(PaymentResponse.From(result.Payment),
            result.Invoice is null ? null : InvoiceResponse.From(result.Invoice));

        return StatusCode(StatusCodes.Status201Created, body);
    }

    /// <summary>
    /// Abre una sesion de Stripe Checkout.
    ///
    /// <para>Devuelve la URL a la que la aplicacion web debe redirigir. Los datos
    /// de la tarjeta se introducen en la pagina de Stripe, nunca en SEMS.</para>
    /// </summary>
    [HttpPost("checkout-session")]
    public async Task<CheckoutSessionResponse> CreateCheckoutSession(
        [FromBody] CreateCheckoutRequest request)
    {
        Guid? subscriptionId = string.IsNullOrWhiteSpace(request.SubscriptionId)
            ? null : Guid.Parse(request.SubscriptionId);

        var session = await _commands.CreateCheckoutSessionAsync(Guid.Parse(request.UserId),
            subscriptionId, request.PlanName, request.Amount, request.Currency,
            request.SuccessUrl, request.CancelUrl);

        return new CheckoutSessionResponse(session.SessionId, session.Url);
    }

    /// <summary>Pagos de un usuario.</summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<List<PaymentResponse>> ByUser(Guid userId) =>
        (await _queries.PaymentsByUserAsync(userId)).Select(PaymentResponse.From).ToList();

    /// <summary>Pagos de una suscripcion.</summary>
    [HttpGet("subscription/{subscriptionId:guid}")]
    public async Task<List<PaymentResponse>> BySubscription(Guid subscriptionId) =>
        (await _queries.PaymentsBySubscriptionAsync(subscriptionId))
        .Select(PaymentResponse.From).ToList();

    /// <summary>Obtiene un pago por su identificador.</summary>
    [HttpGet("{paymentId:guid}")]
    public async Task<PaymentResponse> ById(Guid paymentId) =>
        PaymentResponse.From(await _queries.PaymentByIdAsync(paymentId));
}

/// <summary>Medios de pago guardados del usuario.</summary>
[ApiController]
[Route("api/v1/payment-methods")]
[Tags("Payment Methods")]
public sealed class PaymentMethodController : ControllerBase
{
    private readonly PaymentMethodCommandService _commands;
    private readonly PaymentQueryService _queries;

    public PaymentMethodController(PaymentMethodCommandService commands, PaymentQueryService queries)
    {
        _commands = commands;
        _queries = queries;
    }

    /// <summary>Guarda un medio de pago.</summary>
    [HttpPost]
    public async Task<ActionResult<PaymentMethodResponse>> Register(
        [FromBody] RegisterPaymentMethodRequest request)
    {
        var method = await _commands.RegisterAsync(Guid.Parse(request.UserId), request.Type,
            request.StripePaymentMethodId, request.IsDefault);
        return StatusCode(StatusCodes.Status201Created, PaymentMethodResponse.From(method));
    }

    /// <summary>Medios de pago de un usuario.</summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<List<PaymentMethodResponse>> ByUser(Guid userId) =>
        (await _queries.MethodsByUserAsync(userId)).Select(PaymentMethodResponse.From).ToList();

    /// <summary>Marca un medio de pago como predeterminado.</summary>
    [HttpPut("{paymentMethodId:guid}/default")]
    public async Task<PaymentMethodResponse> SetDefault(Guid paymentMethodId) =>
        PaymentMethodResponse.From(await _commands.SetDefaultAsync(paymentMethodId));

    /// <summary>Elimina un medio de pago.</summary>
    [HttpDelete("{paymentMethodId:guid}")]
    public async Task<IActionResult> Delete(Guid paymentMethodId)
    {
        await _commands.DeleteAsync(paymentMethodId);
        return NoContent();
    }
}

/// <summary>Comprobantes emitidos por cada cobro completado.</summary>
[ApiController]
[Route("api/v1/invoices")]
[Tags("Invoices")]
public sealed class InvoiceController : ControllerBase
{
    private readonly PaymentQueryService _queries;

    public InvoiceController(PaymentQueryService queries) => _queries = queries;

    /// <summary>Obtiene un comprobante por su identificador.</summary>
    [HttpGet("{invoiceId:guid}")]
    public async Task<InvoiceResponse> ById(Guid invoiceId) =>
        InvoiceResponse.From(await _queries.InvoiceByIdAsync(invoiceId));

    /// <summary>Comprobante asociado a un pago.</summary>
    [HttpGet("payment/{paymentId:guid}")]
    public async Task<InvoiceResponse> ByPayment(Guid paymentId) =>
        InvoiceResponse.From(await _queries.InvoiceByPaymentAsync(paymentId));
}

/// <summary>
/// Punto de entrada de los avisos de Stripe.
///
/// <para>El cuerpo se lee como texto crudo a proposito: la verificacion de firma
/// se calcula sobre los bytes exactos que envio Stripe, y cualquier
/// deserializacion intermedia los alteraria y haria fallar la comprobacion.</para>
/// </summary>
[ApiController]
// Stripe no manda JWT: este endpoint se autentica verificando la firma del
// cuerpo. Pedirle sesion haria que los webhooks nunca llegasen.
[AllowAnonymous]
[Route("api/v1/webhooks")]
[Tags("Webhooks")]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly WebhookCommandService _webhooks;

    public StripeWebhookController(WebhookCommandService webhooks) => _webhooks = webhooks;

    /// <summary>Recibe un evento de Stripe.</summary>
    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripe()
    {
        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(new { error = "missing Stripe-Signature header" });
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        var processed = await _webhooks.HandleStripeAsync(payload, signature);

        // Siempre 200: un duplicado no es un error y responder otra cosa haria
        // que Stripe siguiera reintentando indefinidamente.
        return Ok(new { received = true, processed });
    }
}
