Feature: TopConsumerRanking
  In order to identify the root cause of energy waste
  As a facility manager
  I want to see a ranking of the equipment that consumes the most energy

  Scenario: Generating top consumers ranking
    Given the following devices with their monthly consumption:
      | DeviceName           | ConsumptionKWh |
      | Air Conditioner      | 120.5          |
      | Office PC            | 45.3           |
      | Refrigerator         | 80.0           |
    When the system ranks the top consumers
    Then the highest consumer should be "Air Conditioner"
    And the second highest consumer should be "Refrigerator"
