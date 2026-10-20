using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BlazorShared.Models;

namespace WebSpa.Services;

public class OrderService
{
    private readonly HttpClient _httpClient;

    public OrderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Order>> ListMyOrdersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<Order>>("orders");
    }

    public async Task<Order> GetAsync(int orderId)
    {
        var response = await _httpClient.GetAsync($"orders/{orderId}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Order>() : null;
    }
}
