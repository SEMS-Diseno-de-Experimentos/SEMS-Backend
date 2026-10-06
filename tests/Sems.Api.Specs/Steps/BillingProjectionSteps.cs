using TechTalk.SpecFlow;
using Xunit;

namespace Sems.Api.Specs
{
    [Binding]
    public class BillingProjectionSteps
    {
        private decimal _currentUsage;
        private decimal _tariffRate;
        private decimal _projectedAmount;

        [Given(@"the current energy usage is (.*) kWh")]
        public void GivenTheCurrentEnergyUsageIsKWh(decimal usage)
        {
            _currentUsage = usage;
        }

        [Given(@"the commercial tariff rate is (.*) Soles per kWh")]
        public void GivenTheCommercialTariffRateIsSolesPerKWh(decimal rate)
        {
            _tariffRate = rate;
        }

        [When(@"the system calculates the projected bill")]
        public void WhenTheSystemCalculatesTheProjectedBill()
        {
            _projectedAmount = _currentUsage * _tariffRate;
        }

        [Then(@"the projected amount should be (.*) Soles")]
        public void ThenTheProjectedAmountShouldBeSoles(decimal expectedAmount)
        {
            Assert.Equal(expectedAmount, _projectedAmount);
        }
    }
}
