using System.Net;
using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>Pasos que comparten varias features.</summary>
[Binding]
public sealed class CommonSteps
{
    private readonly ApiWorld _world;

    public CommonSteps(ApiWorld world) => _world = world;

    [Given(@"a signed-in user")]
    public async Task GivenASignedInUser() => _world.User = await _world.Factory.CreateUserAsync();

    [Then(@"the request is rejected as a conflict")]
    public void ThenTheRequestIsRejectedAsAConflict() =>
        Assert.Equal(HttpStatusCode.Conflict, _world.Response.StatusCode);

    [Then(@"the request is rejected as invalid")]
    public void ThenTheRequestIsRejectedAsInvalid() =>
        Assert.Equal(HttpStatusCode.BadRequest, _world.Response.StatusCode);

    [Then(@"the request is rejected as invalid with the message ""(.*)""")]
    public void ThenTheRequestIsRejectedAsInvalidWithTheMessage(string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, _world.Response.StatusCode);
        Assert.Equal(message, _world.LastBody.Text("message"));
    }

    [Then(@"the access is rejected as unauthorized")]
    public void ThenTheAccessIsRejectedAsUnauthorized() =>
        Assert.Equal(HttpStatusCode.Unauthorized, _world.Response.StatusCode);
}
