using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CatalogService.Api.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CatalogService.IntegrationTests.CatalogItemEndpoints;

// Flyttet fra tests/PublicApiIntegrationTests/CatalogItemEndpoints/CreateCatalogItemEndpointTest.cs
[TestClass]
public class CreateCatalogItemEndpointTest
{
    private int _testBrandId = 1;
    private int _testTypeId = 2;
    private string _testDescription = "test description";
    private string _testName = "test name";
    private decimal _testPrice = 1.23m;

    [TestMethod]
    public async Task ReturnsNotAuthorizedGivenNormalUserToken()
    {
        // Arrange: almindelig bruger uden Administrators-rollen
        var jsonContent = GetValidNewItemJson();
        var token = ApiTokenHelper.GetNormalUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsync("api/catalog-items", jsonContent);

        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task ReturnsSuccessGivenValidNewItemAndAdminUserToken()
    {
        // Arrange
        var jsonContent = GetValidNewItemJson();
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await client.PostAsync("api/catalog-items", jsonContent);

        // Assert
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<CreateCatalogItemResponse>();

        Assert.AreEqual(_testBrandId, model!.CatalogItem.CatalogBrandId);
        Assert.AreEqual(_testTypeId, model.CatalogItem.CatalogTypeId);
        Assert.AreEqual(_testDescription, model.CatalogItem.Description);
        Assert.AreEqual(_testName, model.CatalogItem.Name);
        Assert.AreEqual(_testPrice, model.CatalogItem.Price);
    }

    private StringContent GetValidNewItemJson()
    {
        var request = new CreateCatalogItemRequest()
        {
            CatalogBrandId = _testBrandId,
            CatalogTypeId = _testTypeId,
            Description = _testDescription,
            Name = _testName,
            Price = _testPrice
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        return jsonContent;
    }
}
