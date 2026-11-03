using CatalogService.Api.Entities;

namespace CatalogService.Api.Interfaces;

/// <summary>
/// Messaging Gateway (EIP): announces catalog changes without the caller knowing about messaging
/// </summary>
public interface ICatalogEvents
{
    Task CatalogItemCreated(CatalogItem item);
    Task CatalogItemUpdated(CatalogItem item);
    Task CatalogItemDeleted(CatalogItem item);
}
