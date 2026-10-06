@EP05 @US27
Feature: Peak hours of the commercial tariff
  Energy consumed between 18:00 and 23:00, local time in Peru, is billed at the
  peak price every day of the year, Sundays included. A site is exempt on
  Sundays only when its supply was granted that exclusion by the distributor.
  No endpoint classifies a single reading, so these scenarios exercise the
  domain rule directly; the published tariff is checked through the API.

  Scenario Outline: A reading is classified into its time band
    Given a site whose supply <exclusion> the Sunday exclusion
    When a reading arrives on <day> at <time> local time
    Then the reading falls in the <band> band

    Examples:
      | exclusion     | day       | time  | band     |
      | does not have | Wednesday | 17:59 | off-peak |
      | does not have | Wednesday | 18:00 | peak     |
      | does not have | Wednesday | 22:59 | peak     |
      | does not have | Wednesday | 23:00 | off-peak |
      | does not have | Sunday    | 20:00 | peak     |
      | has           | Sunday    | 20:00 | off-peak |
      | has           | Saturday  | 20:00 | peak     |
      | does not have | Sunday    | 11:00 | off-peak |

  Scenario: The published tariff states peak hours for every day
    Given a signed-in user
    When the user consults the "MT2" tariff
    Then the peak hours are "18:00-23:00 every day"
