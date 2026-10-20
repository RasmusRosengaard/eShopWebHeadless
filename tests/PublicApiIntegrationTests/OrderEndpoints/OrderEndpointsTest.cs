using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.OrderEndpoints;

/// <summary>
/// Covers checkout and order history that used to live in Web (Checkout page, OrderController).
/// </summary>
[TestClass]
public class OrderEndpointsTest
{
    [TestMethod]
    public async Task CheckoutRequiresToken()
    {
        var response = await ProgramTest.NewClient.PostAsync("api/orders", null);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task CheckoutCreatesOrderAndEmptiesBasket()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        var ordersBefore = await ListOrders(client);

        await client.PostAsync("api/basket/items", new StringContent(new { CatalogItemId = 1, Quantity = 3 }.ToJson(), Encoding.UTF8, "application/json"));
        var checkout = await client.PostAsync("api/orders", null);
        checkout.EnsureSuccessStatusCode();

        var ordersAfter = await ListOrders(client);
        Assert.AreEqual(ordersBefore.Count + 1, ordersAfter.Count);

        var newest = ordersAfter.OrderByDescending(o => o.OrderNumber).First();
        var detailResponse = await client.GetAsync($"api/orders/{newest.OrderNumber}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = (await detailResponse.Content.ReadAsStringAsync()).FromJson<OrderDto>();
        Assert.AreEqual(3, detail!.OrderItems.Single(i => i.ProductId == 1).Units);

        var basketJson = await (await client.GetAsync("api/basket")).Content.ReadAsStringAsync();
        Assert.AreEqual(0, basketJson.FromJson<Microsoft.eShopWeb.PublicApi.BasketEndpoints.BasketDto>()!.Items.Count);
    }

    [TestMethod]
    public async Task CannotReadAnotherUsersOrder()
    {
        var demoUser = ProgramTest.NewClient;
        demoUser.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        await demoUser.PostAsync("api/basket/items", new StringContent(new { CatalogItemId = 2 }.ToJson(), Encoding.UTF8, "application/json"));
        (await demoUser.PostAsync("api/orders", null)).EnsureSuccessStatusCode();
        var orderNumber = (await ListOrders(demoUser)).Max(o => o.OrderNumber);

        var admin = ProgramTest.NewClient;
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetAdminUserToken());
        var response = await admin.GetAsync($"api/orders/{orderNumber}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<List<OrderDto>> ListOrders(HttpClient client)
    {
        var response = await client.GetAsync("api/orders");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsStringAsync()).FromJson<List<OrderDto>>()!;
    }
}
