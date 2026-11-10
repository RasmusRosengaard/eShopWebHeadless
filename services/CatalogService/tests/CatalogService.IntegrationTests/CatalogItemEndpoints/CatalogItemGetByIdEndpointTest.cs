using System.Net;
using CatalogService.Api.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CatalogService.IntegrationTests.CatalogItemEndpoints;

// Flyttet fra tests/PublicApiIntegrationTests/CatalogItemEndpoints/CatalogItemGetByIdEndpointTest.cs
[TestClass]
public class CatalogItemGetByIdEndpointTest
{
    [TestMethod]
    public async Task ReturnsItemGivenValidId()
    {
        // Arrange
        var client = ProgramTest.NewClient;

        // Act
        var response = await client.GetAsync("api/catalog-items/5");

        // Assert
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<GetByIdCatalogItemResponse>();

        Assert.AreEqual(5, model!.CatalogItem.Id);
        Assert.AreEqual("Roslyn Red Sheet", model.CatalogItem.Name);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidId()
    {
        // Arrange
        var client = ProgramTest.NewClient;

        // Act
        var response = await client.GetAsync("api/catalog-items/0");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
