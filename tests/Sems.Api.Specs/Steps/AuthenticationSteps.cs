using TechTalk.SpecFlow;
using Xunit;

namespace Sems.Api.Specs
{
    [Binding]
    public class AuthenticationSteps
    {
        private string _registeredEmail = "";
        private string _registeredPassword = "";
        private bool _loginSuccess;
        private string _accessToken = "";

        [Given(@"a registered user with email ""(.*)"" and password ""(.*)""")]
        public void GivenARegisteredUserWithEmailAndPassword(string email, string password)
        {
            _registeredEmail = email;
            _registeredPassword = password;
        }

        [When(@"the user attempts to log in with ""(.*)"" and ""(.*)""")]
        public void WhenTheUserAttemptsToLogInWithAnd(string email, string password)
        {
            if (email == _registeredEmail && password == _registeredPassword)
            {
                _loginSuccess = true;
                _accessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
            }
            else
            {
                _loginSuccess = false;
                _accessToken = "";
            }
        }

        [Then(@"an access token should be returned")]
        public void ThenAnAccessTokenShouldBeReturned()
        {
            Assert.False(string.IsNullOrEmpty(_accessToken));
        }

        [Then(@"the login status should be successful")]
        public void ThenTheLoginStatusShouldBeSuccessful()
        {
            Assert.True(_loginSuccess);
        }

        [Then(@"no access token should be returned")]
        public void ThenNoAccessTokenShouldBeReturned()
        {
            Assert.True(string.IsNullOrEmpty(_accessToken));
        }

        [Then(@"the login status should fail")]
        public void ThenTheLoginStatusShouldFail()
        {
            Assert.False(_loginSuccess);
        }
    }
}
