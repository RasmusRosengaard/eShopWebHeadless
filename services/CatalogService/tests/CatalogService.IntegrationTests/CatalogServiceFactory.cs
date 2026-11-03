using CatalogService.Api.Data;
using CatalogService.Api.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CatalogService.IntegrationTests;

// Starter CatalogService in-memory med en in-memory database i stedet for Postgres og en mock i stedet for RabbitMQ,
// så testene kan køre i CI uden en database eller broker.
public class CatalogServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CatalogContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CatalogContext>>();
            services.AddDbContext<CatalogContext>(options => options.UseInMemoryDatabase("CatalogServiceTests"));

            services.RemoveAll<ICatalogEvents>();
            services.AddSingleton(Mock.Of<ICatalogEvents>());
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogServiceFactory>>();
        await CatalogContextSeed.SeedAsync(context, logger);
    }
}
