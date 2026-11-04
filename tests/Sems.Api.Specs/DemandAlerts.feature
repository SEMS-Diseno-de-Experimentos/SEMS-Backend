@EP06
Feature: Demand alerts before the contracted power is exceeded
  A demand rule watches a site's demand against its contracted power. It warns
  with headroom once the demand reaches the warning percentage, and raises a
  critical alert when the contracted power is exceeded, so the manager can shed
  load before the excess power charge applies to the whole month.

  Background:
    Given a signed-in manager with a site of 120 kW contracted

  @US28
  Scenario: The manager creates a demand rule
    When the manager creates a demand rule warning at 85 percent
    Then the rule is active with a warning threshold of 102 kW

  @US28
  Scenario Outline: A warning percentage out of range is rejected
    When the manager creates a demand rule warning at <percent> percent
    Then the request is rejected as invalid

    Examples:
      | percent |
      | 0       |
      | 101     |

  @TS05 @US29 @US30 @US31
  Scenario Outline: The measured demand raises the alert that matches its level
    Given the site has a demand rule warning at 85 percent
    When a demand of <demand> kW is evaluated for the site
    Then the resulting alert is "<severity>"

    Examples:
      | demand | severity |
      | 90     | none     |
      | 101.9  | none     |
      | 102    | WARNING  |
      | 120    | WARNING  |
      | 120.1  | CRITICAL |
      | 150    | CRITICAL |

  @US29
  Scenario: The warning tells how much headroom is left
    Given the site has a demand rule warning at 85 percent
    When a demand of 108 kW is evaluated for the site
    Then the resulting alert is "WARNING"
    And the alert reports "12 kW of headroom left"

  @US30
  Scenario: The critical alert tells the excess and notifies the manager
    Given the site has a demand rule warning at 85 percent
    When a demand of 135 kW is evaluated for the site
    Then the resulting alert is "CRITICAL"
    And the alert reports "15 kW above the 120 kW contracted"
    And the manager receives the alert by email
