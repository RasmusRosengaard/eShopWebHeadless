using CatalogService.Api;
using CatalogService.Api.Data;
using CatalogService.Api.Interfaces;
using CatalogService.Api.Services;
using Microsoft.EntityFrameworkCore;
using MinimalApi.Endpoint.Configurations.Extensions;
using MinimalApi.Endpoint.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpoints();

builder.Services.AddHealthChecks();
builder.Services.AddDbContext<CatalogContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
builder.Services.Configure<CatalogSettings>(builder.Configuration);
var catalogSettings = builder.Configuration.Get<CatalogSettings>() ?? new CatalogSettings();
builder.Services.AddSingleton<IUriComposer>(new UriComposer(catalogSettings));

builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapEndpoints();

app.Run();
