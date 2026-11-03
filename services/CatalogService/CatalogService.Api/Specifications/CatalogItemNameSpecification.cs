using Ardalis.Specification;
using CatalogService.Api.Entities;

namespace CatalogService.Api.Specifications;

public class CatalogItemNameSpecification : Specification<CatalogItem>
{
    public CatalogItemNameSpecification(string catalogItemName)
    {
        Query.Where(item => catalogItemName == item.Name);
    }
}
