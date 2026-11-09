using CatalogService.Api;
using CatalogService.Api.Data;
using CatalogService.Api.Interfaces;
using CatalogService.Api.Services;
using Microsoft.EntityFrameworkCore;
using MinimalApi.Endpoint.Configurations.Extensions;
using MinimalApi.Endpoint.Extensions;
using System.Text;
using CatalogService.Api.Middleware;
using CatalogService.Api.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpoints();

builder.Services.AddHealthChecks();
builder.Services.AddDbContext<CatalogContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CatalogConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
builder.Services.Configure<CatalogSettings>(builder.Configuration);
var catalogSettings = builder.Configuration.Get<CatalogSettings>() ?? new CatalogSettings();
builder.Services.AddSingleton<IUriComposer>(new UriComposer(catalogSettings));
var configSection = builder.Configuration.GetRequiredSection(BaseUrlConfiguration.CONFIG_NAME);
builder.Services.Configure<BaseUrlConfiguration>(configSection);

builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

var jwtKey = Encoding.ASCII.GetBytes(builder.Configuration["JwtSecretKey"]!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(config =>
    {
        config.RequireHttpsMetadata = false;
        config.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IEventBus, RabbitMq>();
builder.Services.AddSingleton<ICatalogEvents, CatalogEvents>();

var app = builder.Build();

// Migration og seed må kun køre ét sted ad gangen. I Docker/Swarm starter alle replicas samtidig,
// så det klares af servicen catalog-migrator (samme image, startet med --migrate), som stopper bagefter.
// Lokalt (Development) kører det ved opstart som før, da der kun er én instans.
var migrateOnly = args.Contains("--migrate");
if (migrateOnly || app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CatalogContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (context.Database.IsNpgsql())
    {
        await context.Database.MigrateAsync();
    }
    await CatalogContextSeed.SeedAsync(context, logger);
}

if (migrateOnly)
{
    return;
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapEndpoints();

app.Run();

public partial class Program { }
