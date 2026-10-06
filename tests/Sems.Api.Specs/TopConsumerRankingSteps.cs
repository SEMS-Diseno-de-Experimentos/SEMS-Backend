using System;
using System.Collections.Generic;
using System.Linq;
using TechTalk.SpecFlow;
using Xunit;

namespace Sems.Api.Specs
{
    [Binding]
    public class TopConsumerRankingSteps
    {
        public class DeviceConsumption
        {
            public string DeviceName { get; set; } = string.Empty;
            public decimal ConsumptionKWh { get; set; }
        }

        private List<DeviceConsumption> _devices = new();
        private List<DeviceConsumption> _rankedDevices = new();

        [Given(@"the following devices with their monthly consumption:")]
        public void GivenTheFollowingDevicesWithTheirMonthlyConsumption(Table table)
        {
            foreach (var row in table.Rows)
            {
                _devices.Add(new DeviceConsumption
                {
                    DeviceName = row["DeviceName"],
                    ConsumptionKWh = Convert.ToDecimal(row["ConsumptionKWh"])
                });
            }
        }

        [When(@"the system ranks the top consumers")]
        public void WhenTheSystemRanksTheTopConsumers()
        {
            _rankedDevices = _devices.OrderByDescending(d => d.ConsumptionKWh).ToList();
        }

        [Then(@"the highest consumer should be ""(.*)""")]
        public void ThenTheHighestConsumerShouldBe(string expectedDeviceName)
        {
            Assert.Equal(expectedDeviceName, _rankedDevices[0].DeviceName);
        }

        [Then(@"the second highest consumer should be ""(.*)""")]
        public void ThenTheSecondHighestConsumerShouldBe(string expectedDeviceName)
        {
            Assert.Equal(expectedDeviceName, _rankedDevices[1].DeviceName);
        }
    }
}
