using Ardalis.Specification;

namespace CatalogService.Api.Interfaces;

public interface IRepository<T> : IRepositoryBase<T> where T : class, IAggregateRoot
{
}
