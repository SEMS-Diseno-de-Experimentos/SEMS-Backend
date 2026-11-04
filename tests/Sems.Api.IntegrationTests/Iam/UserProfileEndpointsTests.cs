using System.Net;
using System.Text.Json;
using Sems.Api.TestSupport;

namespace Sems.Api.IntegrationTests.Iam;

/// <summary>
/// Perfil propio: nombre y segmento, de alta a consulta.
/// </summary>
/// <remarks>
/// El cliente pedia los dos en el formulario de registro y los guardaba solo en
/// el navegador. Entrar desde otro equipo dejaba el saludo con la parte local
/// del correo y el segmento en blanco. Ahora los guarda el backend.
/// </remarks>
public class UserProfileEndpointsTests : IClassFixture<SemsApiFactory>
{
    private readonly SemsApiFactory _factory;

    public UserProfileEndpointsTests(SemsApiFactory factory) => _factory = factory;

    private static bool IsEmpty(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    [Fact]
    public async Task Register_WithNameAndSegment_ReturnsThemInTheSession()
    {
        var response = await _factory.CreateClient().PostJsonAsync("/api/v1/auth/register", new
        {
            emailAddress = $"alta-{Guid.NewGuid():N}@energix.test",
            password = "Energix2026",
            fullName = "  Alexis Quispe  ",
            segment = "retail_chain"
        });

        var session = await response.ExpectAsync(HttpStatusCode.OK);
        Assert.Equal("Alexis Quispe", session.Text("fullName"));
        Assert.Equal("RETAIL_CHAIN", session.Text("segment"));
        // El alta ya devuelve token: no hace falta encadenar un login detras.
        Assert.False(string.IsNullOrWhiteSpace(session.Text("token")));
    }

    [Fact]
    public async Task Register_WithoutNameOrSegment_StillWorks()
    {
        var response = await _factory.CreateClient().PostJsonAsync("/api/v1/auth/register", new
        {
            emailAddress = $"alta-{Guid.NewGuid():N}@energix.test",
            password = "Energix2026"
        });

        var session = await response.ExpectAsync(HttpStatusCode.OK);
        Assert.True(IsEmpty(session, "fullName"));
        Assert.True(IsEmpty(session, "segment"));
    }

    [Fact]
    public async Task Register_UnknownSegment_Returns400SayingWhichAreValid()
    {
        var response = await _factory.CreateClient().PostJsonAsync("/api/v1/auth/register", new
        {
            emailAddress = $"alta-{Guid.NewGuid():N}@energix.test",
            password = "Energix2026",
            segment = "FABRICA"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("RETAIL_CHAIN", (await response.ReadJsonAsync()).Text("message"));
    }

    [Fact]
    public async Task Login_AfterRegistering_CarriesTheProfileBack()
    {
        var email = $"alta-{Guid.NewGuid():N}@energix.test";
        var anonymous = _factory.CreateClient();
        await (await anonymous.PostJsonAsync("/api/v1/auth/register", new
        {
            emailAddress = email,
            password = "Energix2026",
            fullName = "Alexis Quispe",
            segment = "INDEPENDENT"
        })).ExpectAsync(HttpStatusCode.OK);

        var session = await (await anonymous.PostJsonAsync("/api/v1/auth/login",
                new { emailAddress = email, password = "Energix2026" }))
            .ExpectAsync(HttpStatusCode.OK);

        Assert.Equal("Alexis Quispe", session.Text("fullName"));
        Assert.Equal("INDEPENDENT", session.Text("segment"));
    }

    [Fact]
    public async Task Me_AfterUpdatingTheProfile_ReturnsTheNewValues()
    {
        var user = await _factory.CreateUserAsync();

        var updated = await (await user.Client.PutJsonAsync("/api/v1/users/me",
                new { fullName = "Alexis Quispe", segment = "retail-chain" }))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal("Alexis Quispe", updated.Text("fullName"));
        Assert.Equal("RETAIL_CHAIN", updated.Text("segment"));

        var me = await (await user.Client.GetAsync("/api/v1/users/me"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.Equal("Alexis Quispe", me.Text("fullName"));
        Assert.Equal("RETAIL_CHAIN", me.Text("segment"));
        Assert.Equal(user.UserId, me.Id("userId"));
    }

    [Fact]
    public async Task UpdateProfile_UnknownSegment_Returns400()
    {
        var user = await _factory.CreateUserAsync();

        var response = await user.Client.PutJsonAsync("/api/v1/users/me",
            new { fullName = "Alexis", segment = "FABRICA" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithoutAToken_Returns401()
    {
        var response = await _factory.CreateClient().PutJsonAsync("/api/v1/users/me",
            new { fullName = "Alexis" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_OnlyChangesTheCaller()
    {
        // El usuario sale del token y no de la ruta: no hay forma de renombrar
        // a otro.
        var one = await _factory.CreateUserAsync();
        var another = await _factory.CreateUserAsync();

        await (await one.Client.PutJsonAsync("/api/v1/users/me", new { fullName = "Uno" }))
            .ExpectAsync(HttpStatusCode.OK);

        var otro = await (await another.Client.GetAsync("/api/v1/users/me"))
            .ExpectAsync(HttpStatusCode.OK);
        Assert.True(IsEmpty(otro, "fullName"));
    }
}
