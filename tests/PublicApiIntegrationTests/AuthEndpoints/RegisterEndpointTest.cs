using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Constants;
using Microsoft.eShopWeb.PublicApi.AuthEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.AuthEndpoints;

/// <summary>
/// Replaces the functional tests of Web's Identity/Account/Register page.
/// </summary>
[TestClass]
public class RegisterEndpointTest
{
    [TestMethod]
    public async Task ReturnsTokenGivenNewUser()
    {
        var email = $"{Guid.NewGuid()}@microsoft.com";

        var response = await Register(email, AuthorizationConstants.DEFAULT_PASSWORD);
        response.EnsureSuccessStatusCode();

        var model = (await response.Content.ReadAsStringAsync()).FromJson<AuthenticateResponse>();
        Assert.IsTrue(model!.Result);
        Assert.AreEqual(email, model.Username);
        Assert.IsFalse(string.IsNullOrEmpty(model.Token));
    }

    [TestMethod]
    public async Task ReturnsBadRequestGivenExistingUser()
    {
        var response = await Register("demouser@microsoft.com", AuthorizationConstants.DEFAULT_PASSWORD);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<HttpResponseMessage> Register(string email, string password) =>
        ProgramTest.NewClient.PostAsync("api/register",
            new StringContent(new RegisterRequest { Email = email, Password = password }.ToJson(), Encoding.UTF8, "application/json"));
}
