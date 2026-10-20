using System;
using System.Net.Http;
using System.Threading.Tasks;
using WebSpa;
using WebSpa.Services;
using Blazored.LocalStorage;
using BlazorShared;
using BlazorShared.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configSection = builder.Configuration.GetRequiredSection(BaseUrlConfiguration.CONFIG_NAME);
builder.Services.Configure<BaseUrlConfiguration>(configSection);
var baseUrlConfig = configSection.Get<BaseUrlConfiguration>();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<AuthTokenStore>();

// All calls go to PublicApi. The handler attaches the JWT and the anonymous basket id.
builder.Services.AddScoped(sp => new HttpClient(new ApiAuthHandler(sp.GetRequiredService<AuthTokenStore>())
{
    InnerHandler = new HttpClientHandler()
})
{
    BaseAddress = new Uri(baseUrlConfig!.ApiBase)
});

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());

builder.Services.AddScoped<BasketState>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ShopCatalogService>();
builder.Services.AddBlazorServices();

builder.Logging.AddConfiguration(builder.Configuration.GetRequiredSection("Logging"));

await ClearLocalStorageCache(builder.Services);

await builder.Build().RunAsync();

static async Task ClearLocalStorageCache(IServiceCollection services)
{
    var sp = services.BuildServiceProvider();
    var localStorageService = sp.GetRequiredService<ILocalStorageService>();

    await localStorageService.RemoveItemAsync(typeof(CatalogBrand).Name);
    await localStorageService.RemoveItemAsync(typeof(CatalogType).Name);
}
