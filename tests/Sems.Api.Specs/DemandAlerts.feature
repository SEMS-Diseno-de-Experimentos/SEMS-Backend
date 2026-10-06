Feature: DemandAlerts
  In order to avoid power penalty costs
  As a facility manager
  I want to be alerted when the demand reaches a certain percentage

  Scenario: High Demand Alert
    Given a commercial facility with a contracted demand of 50 kW
    When the current demand reaches 45 kW
    Then a warning alert should be generated
