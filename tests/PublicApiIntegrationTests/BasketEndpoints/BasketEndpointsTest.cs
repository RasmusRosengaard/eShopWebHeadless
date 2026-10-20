using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.BasketEndpoints;
using Microsoft.eShopWeb.PublicApi.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.BasketEndpoints;

/// <summary>
/// Covers the basket flow that used to live in Web's Basket pages.
/// </summary>
[TestClass]
public class BasketEndpointsTest
{
    [TestMethod]
    public async Task ReturnsBadRequestWithoutBuyerIdentity()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/basket");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task AnonymousBuyerCanAddItemAndReadBasket()
    {
        var client = NewAnonymousClient();

        var addResponse = await client.PostAsync("api/basket/items", Json(new { CatalogItemId = 1, Quantity = 2 }));
        addResponse.EnsureSuccessStatusCode();

        var basket = (await (await client.GetAsync("api/basket")).Content.ReadAsStringAsync()).FromJson<BasketDto>();
        Assert.AreEqual(1, basket!.Items.Count);
        Assert.AreEqual(2, basket.Items[0].Quantity);
        Assert.AreEqual(1, basket.Items[0].CatalogItemId);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenUnknownCatalogItem()
    {
        var response = await NewAnonymousClient().PostAsync("api/basket/items", Json(new { CatalogItemId = 0, Quantity = 1 }));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task SettingQuantityToZeroRemovesItem()
    {
        var client = NewAnonymousClient();
        var added = (await (await client.PostAsync("api/basket/items", Json(new { CatalogItemId = 2 }))).Content.ReadAsStringAsync()).FromJson<BasketDto>();
        var itemId = added!.Items.Single().Id.ToString();

        var response = await client.PutAsync("api/basket/items", Json(new { Quantities = new Dictionary<string, int> { [itemId] = 0 } }));
        response.EnsureSuccessStatusCode();

        var basket = (await response.Content.ReadAsStringAsync()).FromJson<BasketDto>();
        Assert.AreEqual(0, basket!.Items.Count);
    }

    [TestMethod]
    public async Task TransferMovesAnonymousBasketToUser()
    {
        var anonymousClient = NewAnonymousClient();
        await anonymousClient.PostAsync("api/basket/items", Json(new { CatalogItemId = 3 }));
        var anonymousId = anonymousClient.DefaultRequestHeaders.GetValues(BuyerIdentity.ANONYMOUS_ID_HEADER).Single();

        var userClient = ProgramTest.NewClient;
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetAdminUserToken());
        var transfer = await userClient.PostAsync("api/basket/transfer", Json(new { AnonymousId = anonymousId }));
        transfer.EnsureSuccessStatusCode();

        var userBasket = (await (await userClient.GetAsync("api/basket")).Content.ReadAsStringAsync()).FromJson<BasketDto>();
        Assert.IsTrue(userBasket!.Items.Any(i => i.CatalogItemId == 3));

        var anonymousBasket = (await (await anonymousClient.GetAsync("api/basket")).Content.ReadAsStringAsync()).FromJson<BasketDto>();
        Assert.AreEqual(0, anonymousBasket!.Items.Count);
    }

    [TestMethod]
    public async Task TransferRequiresToken()
    {
        var response = await ProgramTest.NewClient.PostAsync("api/basket/transfer", Json(new { AnonymousId = Guid.NewGuid().ToString() }));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpClient NewAnonymousClient()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Add(BuyerIdentity.ANONYMOUS_ID_HEADER, Guid.NewGuid().ToString());
        return client;
    }

    private static StringContent Json(object body) =>
        new(body.ToJson(), Encoding.UTF8, "application/json");
}
