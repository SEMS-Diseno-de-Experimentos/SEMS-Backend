using Microsoft.AspNetCore.Mvc;
using Sems.Api.Modules.Subscriptions.Application;
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

    /// <summary>Lista los planes disponibles.</summary>
    [HttpGet("subscription-plans")]
    public async Task<List<PlanResource>> Plans() =>
        (await _service.ActivePlansAsync()).Select(PlanResource.From).ToList();

    /// <summary>Obtiene un plan por su identificador.</summary>
    [HttpGet("subscription-plans/{planId:guid}")]
    public async Task<PlanResource> PlanById(Guid planId) =>
        PlanResource.From(await _service.PlanByIdAsync(planId));

    // ----------------------------------------------------------- suscripciones

    /// <summary>Suscripciones de un usuario.</summary>
    [HttpGet("subscriptions/users/{userId}")]
    public async Task<List<SubscriptionResource>> ByUser(string userId) =>
        (await _service.SubscriptionsByUserAsync(userId)).Select(SubscriptionResource.From).ToList();

    /// <summary>Obtiene una suscripcion por su identificador.</summary>
    [HttpGet("subscriptions/{subscriptionId:guid}")]
    public async Task<SubscriptionResource> ById(Guid subscriptionId) =>
        SubscriptionResource.From(await _service.SubscriptionByIdAsync(subscriptionId));

    /// <summary>Crea una suscripcion.</summary>
    [HttpPost("subscriptions")]
    public async Task<ActionResult<SubscriptionResource>> Create(
        [FromBody] CreateSubscriptionRequest request)
    {
        var subscription = await _service.CreateAsync(request.UserId, Guid.Parse(request.PlanId), null);
        return StatusCode(StatusCodes.Status201Created, SubscriptionResource.From(subscription));
    }

    /// <summary>Cancela una suscripcion.</summary>
    [HttpPatch("subscriptions/{subscriptionId:guid}/cancel")]
    public async Task<SubscriptionResource> Cancel(Guid subscriptionId) =>
        SubscriptionResource.From(await _service.CancelAsync(subscriptionId));

    /// <summary>Cambia el plan de una suscripcion.</summary>
    [HttpPatch("subscriptions/{subscriptionId:guid}/change-plan")]
    public async Task<SubscriptionResource> ChangePlan(Guid subscriptionId,
        [FromBody] ChangePlanRequest request) =>
        SubscriptionResource.From(
            await _service.ChangePlanAsync(subscriptionId, Guid.Parse(request.NewPlanId)));
}
