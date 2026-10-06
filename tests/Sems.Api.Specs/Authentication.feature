Feature: Authentication
  In order to secure access to my facility data
  As a registered user
  I want to be able to authenticate and receive an access token

  Scenario: Successful login with valid credentials
    Given a registered user with email "admin@energix.com" and password "SecurePassword123"
    When the user attempts to log in with "admin@energix.com" and "SecurePassword123"
    Then an access token should be returned
    And the login status should be successful

  Scenario: Failed login with invalid credentials
    Given a registered user with email "admin@energix.com" and password "SecurePassword123"
    When the user attempts to log in with "admin@energix.com" and "WrongPassword"
    Then no access token should be returned
    And the login status should fail
