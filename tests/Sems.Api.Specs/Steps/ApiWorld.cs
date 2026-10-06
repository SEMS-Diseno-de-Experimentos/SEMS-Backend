using System.Text.Json;
using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>
/// Estado de un escenario, compartido por todas las clases de pasos.
/// </summary>
/// <remarks>
/// SpecFlow crea una instancia por escenario y la inyecta en cada clase de
/// pasos que la pida. La API, en cambio, es de la feature: cada feature tiene
/// su propia instancia y su propia base de datos (ver <see cref="Hooks"/>).
/// </remarks>
public sealed class ApiWorld
{
    public ApiWorld(FeatureContext featureContext)
    {
        Factory = featureContext.Get<SemsApiFactory>();
        Anonymous = Factory.CreateClient();
    }

    public SemsApiFactory Factory { get; }

    /// <summary>Cliente sin sesion.</summary>
    public HttpClient Anonymous { get; }

    /// <summary>Persona que protagoniza el escenario, con su sesion.</summary>
    public TestUser? User { get; set; }

    public TestUser SignedInUser => User ?? throw new InvalidOperationException(
        "El escenario no tiene un usuario con sesion");

    public HttpResponseMessage? LastResponse { get; private set; }

    public JsonElement LastBody { get; private set; }

    public Guid? OrganizationId { get; set; }

    /// <summary>Locales del escenario por su codigo.</summary>
    public Dictionary<string, Guid> Sites { get; } = new();

    /// <summary>Zonas del escenario por su nombre.</summary>
    public Dictionary<string, Guid> Zones { get; } = new();

    /// <summary>Medidores del escenario por su codigo externo.</summary>
    public Dictionary<string, Guid> Submeters { get; } = new();

    /// <summary>Guarda la respuesta como la ultima del escenario.</summary>
    public async Task<HttpResponseMessage> RecordAsync(Task<HttpResponseMessage> call)
    {
        LastResponse = await call;
        LastBody = await LastResponse.ReadJsonAsync();
        return LastResponse;
    }

    public HttpResponseMessage Response => LastResponse ?? throw new InvalidOperationException(
        "El escenario todavia no hizo ninguna peticion");
}
