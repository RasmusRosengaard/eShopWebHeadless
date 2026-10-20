using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.BasketEndpoints;

namespace Microsoft.eShopWeb.PublicApi.Services;

/// <summary>
/// Basket and checkout use cases exposed over HTTP.
/// Moved from Web (BasketViewModelService and the Basket/Index + Basket/Checkout page models).
/// </summary>
public class BasketApiService
{
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IBasketService _basketService;
    private readonly IOrderService _orderService;
    private readonly IUriComposer _uriComposer;

    public BasketApiService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IBasketService basketService,
        IOrderService orderService,
        IUriComposer uriComposer)
    {
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _basketService = basketService;
        _orderService = orderService;
        _uriComposer = uriComposer;
    }

    public async Task<BasketDto> GetBasketAsync(string buyerId)
    {
        var basket = await _basketRepository.FirstOrDefaultAsync(new BasketWithItemsSpecification(buyerId));
        return basket == null ? new BasketDto { BuyerId = buyerId } : await MapAsync(basket);
    }

    public async Task<BasketDto?> AddItemAsync(string buyerId, int catalogItemId, int quantity)
    {
        var catalogItem = await _itemRepository.GetByIdAsync(catalogItemId);
        if (catalogItem == null) return null;

        var basket = await _basketService.AddItemToBasket(buyerId, catalogItemId, catalogItem.Price, quantity);
        return await MapAsync(basket);
    }

    public async Task<BasketDto?> SetQuantitiesAsync(string buyerId, Dictionary<string, int> quantities)
    {
        var basket = await _basketRepository.FirstOrDefaultAsync(new BasketWithItemsSpecification(buyerId));
        if (basket == null) return null;

        var result = await _basketService.SetQuantities(basket.Id, quantities);
        return result.IsSuccess ? await MapAsync(result.Value) : null;
    }

    public Task TransferAsync(string anonymousId, string userName) =>
        _basketService.TransferBasketAsync(anonymousId, userName);

    /// <returns>false if the buyer has no basket.</returns>
    public async Task<bool> CheckoutAsync(string buyerId, Address shippingAddress)
    {
        var basket = await _basketRepository.FirstOrDefaultAsync(new BasketWithItemsSpecification(buyerId));
        if (basket == null) return false;

        // Throws EmptyBasketOnCheckoutException for an empty basket.
        await _orderService.CreateOrderAsync(basket.Id, shippingAddress);
        await _basketService.DeleteBasketAsync(basket.Id);
        return true;
    }

    private async Task<BasketDto> MapAsync(Basket basket)
    {
        var catalogItems = await _itemRepository.ListAsync(
            new CatalogItemsSpecification(basket.Items.Select(i => i.CatalogItemId).ToArray()));

        return new BasketDto
        {
            Id = basket.Id,
            BuyerId = basket.BuyerId,
            Items = basket.Items.Select(basketItem =>
            {
                var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
                return new BasketItemDto
                {
                    Id = basketItem.Id,
                    CatalogItemId = basketItem.CatalogItemId,
                    ProductName = catalogItem.Name,
                    PictureUrl = _uriComposer.ComposePicUri(catalogItem.PictureUri),
                    UnitPrice = basketItem.UnitPrice,
                    Quantity = basketItem.Quantity
                };
            }).ToList()
        };
    }
}
