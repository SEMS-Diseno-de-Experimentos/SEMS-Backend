using System.Net;
using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>Registro, inicio de sesion y recuperacion de contrasena (EP02).</summary>
[Binding]
public sealed class AuthenticationSteps
{
    private const string ResetSubject = "Reset your SEMS password";

    private readonly ApiWorld _world;
    private readonly List<(string Email, HttpResponseMessage Response, string Body)> _recoveries = new();
    private string? _email;

    public AuthenticationSteps(ApiWorld world) => _world = world;

    private Task<HttpResponseMessage> RegisterAsync(string email, string password) =>
        _world.Anonymous.PostJsonAsync("/api/v1/auth/register", new { emailAddress = email, password });

    [Given(@"the email ""(.*)"" is not registered")]
    public async Task GivenTheEmailIsNotRegistered(string email)
    {
        var login = await _world.Anonymous.PostJsonAsync("/api/v1/auth/login",
            new { emailAddress = email, password = SemsApiFactory.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        _email = email;
    }

    [Given(@"an account exists for ""(.*)"" with the password ""(.*)""")]
    public async Task GivenAnAccountExistsFor(string email, string password)
    {
        // Las filas de un Scenario Outline comparten la base de la feature: la
        // cuenta puede existir ya desde la fila anterior.
        var response = await RegisterAsync(email, password);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        _email = email;
    }

    [When(@"the visitor registers with that email and the password ""(.*)""")]
    public Task WhenTheVisitorRegistersWithThatEmail(string password) =>
        _world.RecordAsync(RegisterAsync(_email!, password));

    [When(@"someone signs in as ""(.*)"" with the password ""(.*)""")]
    public Task WhenSomeoneSignsIn(string email, string password) =>
        _world.RecordAsync(_world.Anonymous.PostJsonAsync("/api/v1/auth/login",
            new { emailAddress = email, password }));

    [Then(@"the account is created")]
    public void ThenTheAccountIsCreated()
    {
        Assert.Equal(HttpStatusCode.OK, _world.Response.StatusCode);
        Assert.Equal(_email, _world.LastBody.Text("emailAddress"));
        Assert.NotEqual(Guid.Empty, _world.LastBody.GetProperty("userId").GetGuid());
    }

    [Then(@"a session token is issued")]
    public void ThenASessionTokenIsIssued()
    {
        Assert.Equal(HttpStatusCode.OK, _world.Response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(_world.LastBody.Text("token")));
        Assert.False(string.IsNullOrWhiteSpace(_world.LastBody.Text("refreshToken")));
    }

    [Then(@"the rejection message is ""(.*)""")]
    public void ThenTheRejectionMessageIs(string message) =>
        Assert.Equal(message, _world.LastBody.Text("message"));

    [When(@"password recovery is requested for ""(.*)""")]
    public async Task WhenPasswordRecoveryIsRequestedFor(string email)
    {
        var response = await _world.Anonymous.PostJsonAsync("/api/v1/auth/forgot-password",
            new { emailAddress = email });
        _recoveries.Add((email, response, await response.Content.ReadAsStringAsync()));
    }

    [Then(@"both recovery requests receive the same response")]
    public void ThenBothRecoveryRequestsReceiveTheSameResponse()
    {
        Assert.Equal(2, _recoveries.Count);
        Assert.All(_recoveries, r => Assert.Equal(HttpStatusCode.OK, r.Response.StatusCode));
        Assert.Equal(_recoveries[0].Body, _recoveries[1].Body);
    }

    [Then(@"a reset link is sent only to ""(.*)""")]
    public void ThenAResetLinkIsSentOnlyTo(string email)
    {
        var link = Assert.Single(_world.Factory.Emails.SentTo(email, ResetSubject));
        Assert.Contains("/reset-password?token=", link.Body);

        foreach (var other in _recoveries.Select(r => r.Email).Where(e => e != email))
        {
            Assert.Empty(_world.Factory.Emails.SentTo(other));
        }
    }

    [When(@"a request without a token asks for the current user")]
    public Task WhenARequestWithoutATokenAsksForTheCurrentUser() =>
        _world.RecordAsync(_world.Anonymous.GetAsync("/api/v1/users/me"));
}
