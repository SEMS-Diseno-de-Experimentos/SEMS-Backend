@EP07
Feature: Estimated commercial bill
  The bill of a commercial site adds the energy consumed by time band, the
  power charge on the month's maximum demand and a fixed charge, plus IGV. The
  power charge does not depend on the energy consumed, and the demand above
  the contracted power is billed at a penalty price for the whole month.

  Background:
    Given a signed-in user

  @TS04 @US35
  Scenario: The estimate breaks the bill down by concept
    When the user estimates the "MT2" bill of a site with 250 kW contracted, 12000 kWh at peak, 48000 kWh off peak and a maximum demand of 280 kW
    Then the energy cost is 14868.00
    And the power cost is 17228.00
    And the subtotal is 32108.80
    And the IGV is 5779.58
    And the total is 37888.38

  @US35
  Scenario Outline: The excess is billed only when the maximum demand exceeds the contracted power
    When the user estimates the "MT2" bill of a site with 120 kW contracted, 6000 kWh at peak, 24000 kWh off peak and a maximum demand of <demand> kW
    Then the bill shows a power excess: <excess>
    And the excess power is <excess_kw> kW
    And the power cost is <power_cost>

    Examples:
      | demand | excess | excess_kw | power_cost |
      | 100    | no     | 0         | 5840.00    |
      | 120    | no     | 0         | 7008.00    |
      | 150    | yes    | 30        | 9636.00    |

  @US35 @US36
  Scenario: A single demand peak raises the bill without consuming more energy
    When the user estimates the "MT2" bill of a site with 120 kW contracted, 6000 kWh at peak, 24000 kWh off peak and a maximum demand of 110 kW
    And the user estimates the same bill with a maximum demand of 150 kW
    Then both estimates have the same energy cost
    And the second estimate costs more than 3000 soles extra

  @US36
  Scenario: The estimate reports the share of the subtotal taken by power
    When the user estimates the "MT2" bill of a site with 250 kW contracted, 12000 kWh at peak, 48000 kWh off peak and a maximum demand of 280 kW
    Then the power share of the subtotal is 53.7 percent

  @TS04
  Scenario: An unknown tariff category is rejected
    When the user estimates the "XYZ" bill of a site with 120 kW contracted, 6000 kWh at peak, 24000 kWh off peak and a maximum demand of 110 kW
    Then the request is rejected as invalid
