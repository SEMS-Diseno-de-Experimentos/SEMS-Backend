using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Iam;

/// <summary>
/// Registro, inicio de sesion, recuperacion y ciclo de vida de los tokens a
/// traves de <c>/api/v1/auth</c>. Contrato en camelCase.
/// </summary>
public class AuthenticationEndpointsTests : IClassFixture<SemsApiFactory>
{
    private const string ResetSubject = "Reset your SEMS password";

    private readonly SemsApiFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationEndpointsTests(SemsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"owner-{Guid.NewGuid():N}@energix.test";

    private Task<HttpResponseMessage> RegisterAsync(string email, string password) =>
        _client.PostJsonAsync("/api/v1/auth/register", new { emailAddress = email, password });

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostJsonAsync("/api/v1/auth/login", new { emailAddress = email, password });

    // ---------------------------------------------------------------- registro

    [Fact]
    public async Task Register_NewEmail_Returns200WithTokenAndRefreshToken()
    {
        var response = await RegisterAsync(NewEmail(), "SecurePass123");

        var body = await response.ExpectAsync(HttpStatusCode.OK);
        Assert.False(string.IsNullOrWhiteSpace(body.Text("token")));
        Assert.False(string.IsNullOrWhiteSpace(body.Text("refreshToken")));
        Assert.Equal("STAFF", body.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Register_ExistingEmail_Returns409()
    {
        var email = NewEmail();
        await (await RegisterAsync(email, "SecurePass123")).ExpectAsync(HttpStatusCode.OK);

        var response = await RegisterAsync(email, "AnotherPass456");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "SecurePass123")]
    [InlineData("", "SecurePass123")]
    [InlineData("short.password@energix.test", "short")]
    public async Task Register_InvalidEmailOrShortPassword_Returns400(string email, string password)
    {
        var response = await RegisterAsync(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_NewAccount_StoresBcryptHashAndSha256RefreshToken()
    {
        var email = NewEmail();
        var session = await (await RegisterAsync(email, "SecurePass123")).ExpectAsync(HttpStatusCode.OK);
        var rawRefreshToken = session.Text("refreshToken");

        var user = await _factory.QueryDatabaseAsync(db =>
            db.Set<User>().SingleAsync(u => u.EmailAddress == email));
        var refreshTokens = await _factory.QueryDatabaseAsync(db =>
            db.Set<RefreshToken>().Where(t => t.UserId == user.UserId).ToListAsync());

        Assert.StartsWith("$2", user.PasswordHash);
        Assert.DoesNotContain("SecurePass123", user.PasswordHash);
        var stored = Assert.Single(refreshTokens);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", stored.TokenHash);
        Assert.NotEqual(rawRefreshToken, stored.TokenHash);
    }

    // -------------------------------------------------------- inicio de sesion

    [Fact]
    public async Task Login_ValidCredentials_Returns200()
    {
        var email = NewEmail();
        await (await RegisterAsync(email, "SecurePass123")).ExpectAsync(HttpStatusCode.OK);

        var body = await (await LoginAsync(email, "SecurePass123")).ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(email, body.Text("emailAddress"));
        Assert.False(string.IsNullOrWhiteSpace(body.Text("token")));
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_Return401WithTheSameBody()
    {
        var email = NewEmail();
        await (await RegisterAsync(email, "SecurePass123")).ExpectAsync(HttpStatusCode.OK);

        var wrongPassword = await LoginAsync(email, "WrongPass999");
        var unknownEmail = await LoginAsync(NewEmail(), "SecurePass123");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        // Un cuerpo distinto revelaria que correos estan registrados.
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(),
            await unknownEmail.Content.ReadAsStringAsync());
        Assert.Equal("Invalid credentials", (await wrongPassword.ReadJsonAsync()).Text("message"));
    }

    // ------------------------------------------------------------ recuperacion

    [Fact]
    public async Task ForgotPassword_ExistingEmail_SendsResetLinkOnlyToThatAccount()
    {
        var existing = NewEmail();
        var unknown = NewEmail();
        await (await RegisterAsync(existing, "SecurePass123")).ExpectAsync(HttpStatusCode.OK);

        var forExisting = await _client.PostJsonAsync("/api/v1/auth/forgot-password",
            new { emailAddress = existing });
        var forUnknown = await _client.PostJsonAsync("/api/v1/auth/forgot-password",
            new { emailAddress = unknown });

        Assert.Equal(HttpStatusCode.OK, forExisting.StatusCode);
        Assert.Equal(HttpStatusCode.OK, forUnknown.StatusCode);
        Assert.Equal(await forExisting.Content.ReadAsStringAsync(),
            await forUnknown.Content.ReadAsStringAsync());

        var resetEmail = Assert.Single(_factory.Emails.SentTo(existing, ResetSubject));
        Assert.Contains("/reset-password?token=", resetEmail.Body);
        Assert.Empty(_factory.Emails.SentTo(unknown));
    }

    // ------------------------------------------------------------------ tokens

    [Fact]
    public async Task Refresh_SameTokenTwice_SecondReturns401()
    {
        var user = await _factory.CreateUserAsync();

        var first = await _client.PostJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = user.RefreshToken });
        var second = await _client.PostJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = user.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Logout_RefreshToken_Returns204AndTokenNoLongerRefreshes()
    {
        var user = await _factory.CreateUserAsync();

        var logout = await user.Client.PostJsonAsync("/api/v1/auth/logout",
            new { refreshToken = user.RefreshToken });
        var refresh = await _client.PostJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = user.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    // --------------------------------------------------------- recurso protegido

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.c2lnbmF0dXJl")]
    public async Task GetMe_WithoutTokenOrWithMalformedToken_Returns401(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ValidToken_Returns200WithTheTokenOwner()
    {
        var user = await _factory.CreateUserAsync();

        var body = await (await user.Client.GetAsync("/api/v1/users/me")).ExpectAsync(HttpStatusCode.OK);

        Assert.Equal(user.UserId, body.GetProperty("userId").GetGuid());
        Assert.Equal(user.Email, body.Text("emailAddress"));
    }
}
