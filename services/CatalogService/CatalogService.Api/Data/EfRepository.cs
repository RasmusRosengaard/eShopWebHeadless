using Ardalis.Specification.EntityFrameworkCore;
using CatalogService.Api.Interfaces;

namespace CatalogService.Api.Data;

public class EfRepository<T> : RepositoryBase<T>, IReadRepository<T>, IRepository<T> where T : class, IAggregateRoot
{
    public EfRepository(CatalogContext dbContext) : base(dbContext)
    {
    }
}
