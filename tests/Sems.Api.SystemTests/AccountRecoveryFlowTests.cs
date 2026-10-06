using System.Net;
using System.Text.RegularExpressions;
using Sems.Api.TestSupport;

namespace Sems.Api.SystemTests;

/// <summary>
/// Flujo 4: recuperacion de la cuenta (US07, US08, US09).
/// </summary>
/// <remarks>
/// Encadena IAM con el correo que dispara <c>PasswordResetRequested</c>: el
/// enlace se toma del correo enviado, como lo haria la persona.
/// </remarks>
public class AccountRecoveryFlowTests : IClassFixture<SemsApiFactory>
{
    private const string ResetSubject = "Reset your SEMS password";

    private readonly SemsApiFactory _factory;

    public AccountRecoveryFlowTests(SemsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AccountRecovery_ResetThroughTheEmailedLink_OnlyTheNewPasswordSignsIn()
    {
        var user = await _factory.CreateUserAsync(password: "SecurePass123");
        var anonymous = _factory.CreateClient();

        // US08: pide recuperar la contrasena y el enlace llega por correo.
        await (await anonymous.PostJsonAsync("/api/v1/auth/forgot-password",
            new { emailAddress = user.Email })).ExpectAsync(HttpStatusCode.OK);
        var email = Assert.Single(_factory.Emails.SentTo(user.Email, ResetSubject));
        var token = Regex.Match(email.Body, @"token=([A-Za-z0-9_\-]+)").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "el correo no trae el enlace de recuperacion");

        // Cambia la contrasena con el token del enlace.
        var reset = await (await anonymous.PostJsonAsync("/api/v1/auth/reset-password",
            new { token, newPassword = "BrandNewPass456" })).ExpectAsync(HttpStatusCode.OK);
        Assert.Equal("Password updated successfully.", reset.Text("message"));

        // El enlace no sirve una segunda vez.
        var reused = await anonymous.PostJsonAsync("/api/v1/auth/reset-password",
            new { token, newPassword = "AnotherPass789" });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        // US09: el token de refresco anterior quedo revocado.
        var refresh = await anonymous.PostJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = user.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // US07: solo la nueva contrasena permite iniciar sesion.
        var withOld = await anonymous.PostJsonAsync("/api/v1/auth/login",
            new { emailAddress = user.Email, password = "SecurePass123" });
        var withNew = await anonymous.PostJsonAsync("/api/v1/auth/login",
            new { emailAddress = user.Email, password = "BrandNewPass456" });
        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
    }
}
