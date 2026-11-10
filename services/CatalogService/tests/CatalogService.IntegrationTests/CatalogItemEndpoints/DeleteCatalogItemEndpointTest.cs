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
        // Arrange
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await client.DeleteAsync("api/catalog-items/12");

        // Assert
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<DeleteCatalogItemResponse>();

        Assert.AreEqual("Deleted", model!.Status);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidIdAndAdminUserToken()
    {
        // Arrange
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await client.DeleteAsync("api/catalog-items/0");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
