using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Subscriptions.Application;
using Sems.Api.Shared.Http;
using Sems.Api.Shared.Security;
using static Sems.Api.Modules.Subscriptions.Interfaces.SubscriptionResources;

namespace Sems.Api.Modules.Subscriptions.Interfaces;

/// <summary>
/// API REST del bounded context de suscripciones.
///
/// <para>Rutas identicas a las del microservicio en Go. Ojo con el orden de los
/// segmentos: <c>/subscriptions/users/{userId}</c> debe declararse antes que
/// <c>/subscriptions/{subscriptionId}</c> para que "users" no se interprete
/// como un identificador. La restriccion <c>:guid</c> lo garantiza ademas por
/// tipo.</para>
/// </summary>
[ApiController]
[Route("api/v1")]
[Tags("Subscriptions")]
public sealed class SubscriptionController : ControllerBase
{
    private readonly SubscriptionService _service;

    public SubscriptionController(SubscriptionService service) => _service = service;

    // ------------------------------------------------------------------ planes

    /// <summary>Lists the available plans.</summary>
    [HttpGet("subscription-plans")]
    public async Task<List<PlanResource>> Plans() =>
        (await _service.ActivePlansAsync()).Select(PlanResource.From).ToList();

    /// <summary>Gets a plan by its identifier.</summary>
    [HttpGet("subscription-plans/{planId:guid}")]
    public async Task<PlanResource> PlanById(Guid planId) =>
        PlanResource.From(await _service.PlanByIdAsync(planId));

    // ----------------------------------------------------------- suscripciones

    /// <summary>A user's subscriptions.</summary>
    [HttpGet("subscriptions/users/{userId}")]
    public async Task<List<SubscriptionResource>> ByUser(string userId)
    {
        User.MustBeSelf(userId);
        return (await _service.SubscriptionsByUserAsync(userId))
            .Select(SubscriptionResource.From).ToList();
    }

    /// <summary>Gets a subscription by its identifier.</summary>
    /// <remarks>
    /// La suscripcion es de una persona y no de un local, asi que aqui no hay
    /// membresia a la que recurrir: o es suya, o no la ve.
    /// </remarks>
    [HttpGet("subscriptions/{subscriptionId:guid}")]
    public async Task<SubscriptionResource> ById(Guid subscriptionId)
    {
        var suscripcion = await _service.SubscriptionByIdAsync(subscriptionId);
        AccessGuards.MustOwn(User.CallerId(), suscripcion.UserId);
        return SubscriptionResource.From(suscripcion);
    }

    /// <summary>Creates a subscription.</summary>
    [HttpPost("subscriptions")]
    public async Task<ActionResult<SubscriptionResource>> Create(
        [FromBody] CreateSubscriptionRequest request)
    {
        // Suscribirse a nombre de otro le cargaria el plan a quien no lo pidio.
        User.MustBeSelf(request.UserId);
        var subscription = await _service.CreateAsync(request.UserId, Guid.Parse(request.PlanId), null);
        return StatusCode(StatusCodes.Status201Created, SubscriptionResource.From(subscription));
    }

    /// <summary>Cancels a subscription.</summary>
    [HttpPatch("subscriptions/{subscriptionId:guid}/cancel")]
    public async Task<SubscriptionResource> Cancel(Guid subscriptionId)
    {
        await DebeSerSuyaAsync(subscriptionId);
        return SubscriptionResource.From(await _service.CancelAsync(subscriptionId));
    }

    /// <summary>Changes the plan of a subscription.</summary>
    [HttpPatch("subscriptions/{subscriptionId:guid}/change-plan")]
    public async Task<SubscriptionResource> ChangePlan(Guid subscriptionId,
        [FromBody] ChangePlanRequest request)
    {
        await DebeSerSuyaAsync(subscriptionId);
        return SubscriptionResource.From(
            await _service.ChangePlanAsync(subscriptionId, Guid.Parse(request.NewPlanId)));
    }

    /// <summary>
    /// Comprueba la propiedad antes de escribir.
    /// </summary>
    /// <remarks>
    /// Cuesta una lectura de mas: el servicio volvera a cargar la suscripcion
    /// para modificarla. Es el precio de dejar la capa de aplicacion intacta,
    /// que es la que usan el sembrador y los manejadores de eventos, que no
    /// actuan en nombre de nadie y no tienen token que comprobar.
    /// </remarks>
    private async Task DebeSerSuyaAsync(Guid subscriptionId) =>
        AccessGuards.MustOwn(User.CallerId(),
            (await _service.SubscriptionByIdAsync(subscriptionId)).UserId);
}
