using System.Collections.Generic;

namespace BlazorShared.Models;

public class Basket
{
    public int Id { get; set; }
    public string BuyerId { get; set; }
    public List<BasketItem> Items { get; set; } = new();
    public decimal Total { get; set; }
}

public class BasketItem
{
    public int Id { get; set; }
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; }
    public string PictureUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
