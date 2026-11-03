using CatalogService.Api.Interfaces;

namespace CatalogService.Api.Services;

public class UriComposer : IUriComposer
{
    private readonly CatalogSettings _catalogSettings;

    public UriComposer(CatalogSettings catalogSettings) => _catalogSettings = catalogSettings;

    public string ComposePicUri(string uriTemplate)
    {
        return uriTemplate.Replace("http://catalogbaseurltobereplaced", _catalogSettings.CatalogBaseUrl);
    }
}
