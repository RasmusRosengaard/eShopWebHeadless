using CatalogService.Api.Entities;
using CatalogService.Api.Interfaces;

namespace CatalogService.Api.Messaging;

public class CatalogEvents : ICatalogEvents
{
    private readonly IEventBus _eventBus;
    private readonly IUriComposer _uriComposer;

    public CatalogEvents(IEventBus eventBus, IUriComposer uriComposer)
    {
        _eventBus = eventBus;
        _uriComposer = uriComposer;
    }

    public async Task CatalogItemCreated(CatalogItem item)
    {
        var message = new
        {
            eventId = Guid.NewGuid(),
            occurredAt = DateTimeOffset.UtcNow,
            version = item.Version,
            item = ToData(item)
        };

        await _eventBus.Publish("catalog.item.created", message);
    }

    public async Task CatalogItemUpdated(CatalogItem item)
    {
        var message = new
        {
            eventId = Guid.NewGuid(),
            occurredAt = DateTimeOffset.UtcNow,
            version = item.Version,
            item = ToData(item)
        };

        await _eventBus.Publish("catalog.item.updated", message);
    }

    public async Task CatalogItemDeleted(CatalogItem item)
    {
        var message = new
        {
            eventId = Guid.NewGuid(),
            occurredAt = DateTimeOffset.UtcNow,
            version = item.Version + 1,
            id = item.Id
        };

        await _eventBus.Publish("catalog.item.deleted", message);
    }

    private object ToData(CatalogItem item)
    {
        return new
        {
            id = item.Id,
            name = item.Name,
            description = item.Description,
            price = item.Price,
            pictureUri = _uriComposer.ComposePicUri(item.PictureUri),
            catalogTypeId = item.CatalogTypeId,
            catalogBrandId = item.CatalogBrandId
        };
    }
}
