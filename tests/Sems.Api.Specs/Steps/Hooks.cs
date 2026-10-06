using Sems.Api.TestSupport;
using TechTalk.SpecFlow;

namespace Sems.Api.Specs.Steps;

/// <summary>
/// Cada feature levanta su propia API con su propia base de datos SQLite en
/// memoria, y la apaga al terminar.
/// </summary>
[Binding]
public sealed class Hooks
{
    [BeforeFeature]
    public static void StartApi(FeatureContext featureContext) =>
        featureContext.Set(new SemsApiFactory());

    [AfterFeature]
    public static void StopApi(FeatureContext featureContext)
    {
        if (featureContext.TryGetValue(out SemsApiFactory factory))
        {
            factory.Dispose();
        }
    }
}
