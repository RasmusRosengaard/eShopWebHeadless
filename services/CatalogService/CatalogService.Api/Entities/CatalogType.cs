using CatalogService.Api.Interfaces;

namespace CatalogService.Api.Entities;

public class CatalogType : BaseEntity, IAggregateRoot
{
    public string Type { get; private set; }
    public CatalogType(string type)
    {
        Type = type;
    }
}
