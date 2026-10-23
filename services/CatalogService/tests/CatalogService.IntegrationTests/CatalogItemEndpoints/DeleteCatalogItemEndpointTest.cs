using System.Net;
using System.Net.Http.Headers;
using CatalogService.Api.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CatalogService.IntegrationTests.CatalogItemEndpoints;

// Flyttet fra tests/PublicApiIntegrationTests/CatalogItemEndpoints/DeleteCatalogItemEndpointTest.cs
[TestClass]
public class DeleteCatalogItemEndpointTest
{
    [TestMethod]
    public async Task ReturnsSuccessGivenValidIdAndAdminUserToken()
    {
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.DeleteAsync("api/catalog-items/12");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<DeleteCatalogItemResponse>();

        Assert.AreEqual("Deleted", model!.Status);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidIdAndAdminUserToken()
    {
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.DeleteAsync("api/catalog-items/0");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
