using Ardalis.Specification;

namespace CatalogService.Api.Interfaces;

public interface IReadRepository<T> : IReadRepositoryBase<T> where T : class, IAggregateRoot
{
}
