using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BlazorShared.Models;

namespace WebSpa.Services;

/// <summary>
/// Catalog browsing for the shop (filter + paging). Replaces Web's CatalogViewModelService.
/// </summary>
public class ShopCatalogService
{
    private readonly HttpClient _httpClient;

    public ShopCatalogService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedCatalogItemResponse> ListPagedAsync(int pageIndex, int pageSize, int? brandId, int? typeId)
    {
        var url = $"catalog-items?pageIndex={pageIndex}&pageSize={pageSize}";
        if (brandId.HasValue) url += $"&catalogBrandId={brandId}";
        if (typeId.HasValue) url += $"&catalogTypeId={typeId}";

        return await _httpClient.GetFromJsonAsync<PagedCatalogItemResponse>(url);
    }
}
