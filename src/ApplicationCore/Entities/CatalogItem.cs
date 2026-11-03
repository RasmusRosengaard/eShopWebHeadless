using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities;

public class CatalogItem : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public string PictureUri { get; private set; }
    public int CatalogTypeId { get; private set; }
    public CatalogType? CatalogType { get; private set; }
    public int CatalogBrandId { get; private set; }
    public CatalogBrand? CatalogBrand { get; private set; }

    // Sync med CatalogService: Version er itemets version i CatalogService, IsDeleted markerer et slettet item (tombstone).
    public int Version { get; private set; } = 1;
    public bool IsDeleted { get; private set; }

    public CatalogItem(int catalogTypeId,
        int catalogBrandId,
        string description,
        string name,
        decimal price,
        string pictureUri)
    {
        CatalogTypeId = catalogTypeId;
        CatalogBrandId = catalogBrandId;
        Description = description;
        Name = name;
        Price = price;
        PictureUri = pictureUri;
    }

    // Opretter monolittens kopi af et item fra CatalogService med samme id,
    // så kurve og ordrer peger på det samme item i begge databaser.
    public static CatalogItem FromCatalogService(int id,
        int catalogTypeId,
        int catalogBrandId,
        string description,
        string name,
        decimal price,
        string pictureUri,
        int version)
    {
        var item = new CatalogItem(catalogTypeId, catalogBrandId, description, name, price, pictureUri);
        item.Id = id;
        item.Version = version;
        return item;
    }

    // Overskriver kopien med den nye tilstand fra CatalogService (push model: eventet bærer hele itemet).
    public void SyncFromCatalogService(int catalogTypeId,
        int catalogBrandId,
        string description,
        string name,
        decimal price,
        string pictureUri,
        int version)
    {
        CatalogTypeId = catalogTypeId;
        CatalogBrandId = catalogBrandId;
        Description = description;
        Name = name;
        Price = price;
        PictureUri = pictureUri;
        Version = version;
    }

    // Rækken bliver liggende, så et forsinket created/updated med lavere version kan afvises.
    public void MarkAsDeleted(int version)
    {
        IsDeleted = true;
        Version = version;
    }
}
