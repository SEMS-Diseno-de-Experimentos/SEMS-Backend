Feature: BillingProjection
  In order to forecast my monthly expenses
  As a facility manager
  I want to see my energy consumption translated into local currency (Soles)

  Scenario: Projecting monthly bill based on current usage
    Given the current energy usage is 1000 kWh
    And the commercial tariff rate is 0.65 Soles per kWh
    When the system calculates the projected bill
    Then the projected amount should be 650.00 Soles
