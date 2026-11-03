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
var baseUrlConfig = configSection.Get<BaseUrlConfiguration>();

const string CORS_POLICY = "CorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: CORS_POLICY,
        corsPolicyBuilder =>
        {
            // Two headless clients call the API: the shop (WebSpa) and BlazorAdmin.
            corsPolicyBuilder.WithOrigins(
                baseUrlConfig!.WebBase.Replace("host.docker.internal", "localhost").TrimEnd('/'),
                baseUrlConfig.AdminBase.Replace("host.docker.internal", "localhost").TrimEnd('/'));
            corsPolicyBuilder.AllowAnyMethod();
            corsPolicyBuilder.AllowAnyHeader();
        });
});
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
builder.Services.AddSingleton<RabbitMq>();
builder.Services.AddSingleton<ICatalogEvents, CatalogEvents>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (context.Database.IsNpgsql())
    {
        await context.Database.MigrateAsync();
    }
    await CatalogContextSeed.SeedAsync(context, logger);
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors(CORS_POLICY);

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapEndpoints();

app.Run();

public partial class Program { }
