@EP03
Feature: Organization, site and zone registration
  An administrator registers the business with its tax id (RUC) and then each
  physical site with its contracted power and tariff category, so that every
  bill estimate uses the tariff the site actually pays. Sites are divided into
  zones to know where the energy is consumed.

  Background:
    Given an administrator is signed in

  @US12 @TS03
  Scenario: The administrator registers an organization with a valid RUC
    When the administrator registers the organization "Minimarket Los Andes S.A.C." with the RUC "20601234567"
    Then the organization is created
    And the administrator holds the "ORG_ADMIN" role in it

  @US12
  Scenario Outline: An organization with a malformed RUC is rejected
    When the administrator registers the organization "Bodega Central" with the RUC "<ruc>"
    Then the request is rejected as invalid with the message "tax_id must be 11 digits"

    Examples:
      | case        | ruc          |
      | ten digits  | 2060123456   |
      | twelve      | 206012345678 |
      | with letter | 2060123456A  |

  @US12
  Scenario: An organization with a RUC already registered is rejected
    Given an organization with the RUC "20609876543" already exists
    When the administrator registers the organization "Copycat S.A.C." with the RUC "20609876543"
    Then the request is rejected as a conflict

  @US13 @TS03
  Scenario Outline: The administrator registers a site with its supply data
    Given the administrator has registered an organization
    When the administrator registers the site "<code>" with <power> kW contracted under the tariff "<tariff>"
    Then the site is registered in the organization
    And the site charges for demand: <charges>

    Examples:
      | code  | power | tariff | charges |
      | T-001 | 15    | BT5B   | no      |
      | T-002 | 60    | BT3    | yes     |
      | T-003 | 250   | MT2    | yes     |

  @US13
  Scenario: A site code already used in the organization is rejected
    Given the administrator has registered an organization
    And the organization has the site "T-001"
    When the administrator registers the site "T-001" with 120 kW contracted under the tariff "MT2"
    Then the request is rejected as a conflict

  @US13
  Scenario Outline: A site without a positive contracted power is rejected
    Given the administrator has registered an organization
    When the administrator registers the site "T-009" with <power> kW contracted under the tariff "MT2"
    Then the request is rejected as invalid

    Examples:
      | power |
      | 0     |
      | -10   |

  @US14 @US16
  Scenario: Archived sites no longer appear in the site list
    Given the administrator has registered an organization
    And the organization has the site "T-001"
    And the organization has the site "T-002"
    When the administrator archives the site "T-002"
    Then the site list contains only "T-001"

  @US18
  Scenario Outline: A zone deduces whether it operates outside opening hours
    Given the administrator has registered an organization
    And the organization has the site "T-001"
    When the administrator registers the zone "<name>" of type "<type>" in the site "T-001"
    Then the zone is registered in that site
    And the zone operates off hours: <off_hours>

    Examples:
      | name        | type         | off_hours |
      | Cold rooms  | COLD_STORAGE | yes       |
      | Sales floor | SALES_FLOOR  | no        |
