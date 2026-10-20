using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Replaces Web's OrderViewModel / OrderDetailViewModel.
/// </summary>
public class OrderDto
{
    private const string DEFAULT_STATUS = "Pending";

    public int OrderNumber { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public decimal Total { get; set; }
    public string Status => DEFAULT_STATUS;
    public Address? ShippingAddress { get; set; }
    public List<OrderItemDto> OrderItems { get; set; } = new();

    public static OrderDto FromOrder(Order order, bool includeItems) => new()
    {
        OrderNumber = order.Id,
        OrderDate = order.OrderDate,
        Total = order.Total(),
        ShippingAddress = order.ShipToAddress,
        OrderItems = includeItems
            ? order.OrderItems.Select(oi => new OrderItemDto
            {
                ProductId = oi.ItemOrdered.CatalogItemId,
                ProductName = oi.ItemOrdered.ProductName,
                PictureUrl = oi.ItemOrdered.PictureUri,
                UnitPrice = oi.UnitPrice,
                Units = oi.Units
            }).ToList()
            : new()
    };
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string PictureUrl { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}
