@EP04
Feature: Submeter registration and removal
  A supervisor registers each submeter in an active site and, optionally, in
  one of that site's zones. Removing a submeter is logical: it leaves the
  listings and stops counting towards the plan, but its history is kept.

  Background:
    Given a signed-in supervisor with the site "T-001" and its zone "Cold rooms"

  @US19
  Scenario: The supervisor registers a submeter in a zone of the site
    When the supervisor registers the submeter "SM-0001" in the site "T-001" and the zone "Cold rooms"
    Then the submeter is registered as "ACTIVE"
    And the submeter belongs to the site "T-001" and the zone "Cold rooms"

  @US19
  Scenario: A zone that belongs to another site is rejected
    Given the organization also has the site "T-002" and its zone "Kitchen"
    When the supervisor registers the submeter "SM-0002" in the site "T-001" and the zone "Kitchen"
    Then the request is rejected as invalid

  @US19
  Scenario: A duplicated submeter code is rejected
    Given the submeter "SM-0003" is registered in the site "T-001"
    When the supervisor registers the submeter "SM-0003" in the site "T-001" and the zone "Cold rooms"
    Then the request is rejected as a conflict

  @US20 @US23
  Scenario: A removed submeter leaves the listings but keeps its history
    Given the submeter "SM-0004" is registered in the site "T-001"
    And the submeter "SM-0005" is registered in the site "T-001"
    When the supervisor removes the submeter "SM-0005"
    Then the site "T-001" lists only the submeter "SM-0004"
    And the submeter "SM-0005" is kept with the status "REMOVED"
