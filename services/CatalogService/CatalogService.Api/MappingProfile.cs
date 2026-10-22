using AutoMapper;
using CatalogService.Api.Entities;
using CatalogService.Api.CatalogBrandEndpoints;
using CatalogService.Api.CatalogItemEndpoints;
using CatalogService.Api.CatalogTypeEndpoints;

namespace CatalogService.Api;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CatalogItem, CatalogItemDto>();
        CreateMap<CatalogType, CatalogTypeDto>()
            .ForMember(dto => dto.Name, options => options.MapFrom(src => src.Type));
        CreateMap<CatalogBrand, CatalogBrandDto>()
            .ForMember(dto => dto.Name, options => options.MapFrom(src => src.Brand));
    }
}
