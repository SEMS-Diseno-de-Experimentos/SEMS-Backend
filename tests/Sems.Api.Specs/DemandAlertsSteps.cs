using TechTalk.SpecFlow;
using Xunit;

namespace Sems.Api.Specs
{
    [Binding]
    public class DemandAlertsSteps
    {
        private decimal _contractedDemand;
        private decimal _currentDemand;
        private bool _alertGenerated;

        [Given(@"a commercial facility with a contracted demand of (.*) kW")]
        public void GivenACommercialFacilityWithAContractedDemandOfKW(decimal contractedDemand)
        {
            _contractedDemand = contractedDemand;
        }

        [When(@"the current demand reaches (.*) kW")]
        public void WhenTheCurrentDemandReachesKW(decimal currentDemand)
        {
            _currentDemand = currentDemand;
            
            // Basic logic representing the domain rule for warnings
            if (_currentDemand >= _contractedDemand * 0.85m)
            {
                _alertGenerated = true;
            }
        }

        [Then(@"a warning alert should be generated")]
        public void ThenAWarningAlertShouldBeGenerated()
        {
            Assert.True(_alertGenerated);
        }
    }
}
