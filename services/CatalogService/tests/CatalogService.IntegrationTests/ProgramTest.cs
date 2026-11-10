using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CatalogService.IntegrationTests;

[TestClass]
public class ProgramTest
{
    private static CatalogServiceFactory _application = new();

    public static HttpClient NewClient
    {
        get
        {
            return _application.CreateClient();
        }
    }

    public static IServiceProvider Services
    {
        get
        {
            return _application.Services;
        }
    }

    [AssemblyInitialize]
    public static async Task AssemblyInitialize(TestContext _)
    {
        _application = new CatalogServiceFactory();
        await _application.SeedAsync();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        _application.Dispose();
    }
}
