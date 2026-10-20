using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BlazorShared.Models;

namespace WebSpa.Services;

/// <summary>
/// Client side basket. Holds the latest basket so the header badge and the basket page stay in sync.
/// </summary>
public class BasketState
{
    private readonly HttpClient _httpClient;

    public BasketState(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Basket Basket { get; private set; } = new();
    public int ItemCount => Basket.Items.Sum(i => i.Quantity);
    public event Action OnChange;

    public async Task RefreshAsync()
    {
        Set(await _httpClient.GetFromJsonAsync<Basket>("basket"));
    }

    public async Task AddItemAsync(int catalogItemId, int quantity = 1)
    {
        var response = await _httpClient.PostAsJsonAsync("basket/items", new { CatalogItemId = catalogItemId, Quantity = quantity });
        response.EnsureSuccessStatusCode();
        Set(await response.Content.ReadFromJsonAsync<Basket>());
    }

    /// <param name="quantities">Basket item id -> quantity. 0 removes the item.</param>
    public async Task UpdateQuantitiesAsync(Dictionary<string, int> quantities)
    {
        var response = await _httpClient.PutAsJsonAsync("basket/items", new { Quantities = quantities });
        response.EnsureSuccessStatusCode();
        Set(await response.Content.ReadFromJsonAsync<Basket>());
    }

    /// <returns>true if an order was created.</returns>
    public async Task<bool> CheckoutAsync()
    {
        var response = await _httpClient.PostAsync("orders", null);
        await RefreshAsync();
        return response.IsSuccessStatusCode;
    }

    private void Set(Basket basket)
    {
        Basket = basket ?? new Basket();
        OnChange?.Invoke();
    }
}
