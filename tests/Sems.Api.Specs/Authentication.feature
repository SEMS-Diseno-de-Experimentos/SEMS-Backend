@EP02
Feature: Account registration and sign-in
  The platform keeps every establishment's data behind an account. A visitor
  registers with an email and a password, and a registered user signs in to
  receive a session token. Neither sign-in nor password recovery reveals
  whether an email is registered.

  @US06 @TS01
  Scenario: A visitor registers with an unused email
    Given the email "maria.quispe@minimarket.pe" is not registered
    When the visitor registers with that email and the password "SecurePass123"
    Then the account is created
    And a session token is issued

  @US06
  Scenario: A visitor registers with an email already in use
    Given an account exists for "carlos.rojas@bakery.pe" with the password "SecurePass123"
    When the visitor registers with that email and the password "AnotherPass456"
    Then the request is rejected as a conflict

  @US07 @TS01
  Scenario: A registered user signs in with valid credentials
    Given an account exists for "ana.torres@gym.pe" with the password "SecurePass123"
    When someone signs in as "ana.torres@gym.pe" with the password "SecurePass123"
    Then a session token is issued

  @US07 @TS01
  Scenario Outline: Sign-in is rejected without revealing whether the email exists
    Given an account exists for "luis.vega@pharmacy.pe" with the password "SecurePass123"
    When someone signs in as "<email>" with the password "<password>"
    Then the access is rejected as unauthorized
    And the rejection message is "Invalid credentials"

    Examples:
      | case               | email                 | password      |
      | wrong password     | luis.vega@pharmacy.pe | WrongPass999  |
      | unregistered email | nobody@pharmacy.pe    | SecurePass123 |

  @US08
  Scenario: Password recovery answers the same whether the account exists or not
    Given an account exists for "rosa.diaz@restaurant.pe" with the password "SecurePass123"
    When password recovery is requested for "rosa.diaz@restaurant.pe"
    And password recovery is requested for "ghost@restaurant.pe"
    Then both recovery requests receive the same response
    And a reset link is sent only to "rosa.diaz@restaurant.pe"

  @TS02
  Scenario: A protected endpoint rejects a request without a token
    When a request without a token asks for the current user
    Then the access is rejected as unauthorized
