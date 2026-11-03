var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// CORS håndteres her i gatewayen for alle services, så browseren får svar på preflights,
// også selvom den service, routen peger på, er nede. Services skal derfor ikke selv have CORS.
const string CORS_POLICY = "CorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: CORS_POLICY,
        corsPolicyBuilder =>
        {
            // Two headless clients call the API: the shop (WebSpa) and BlazorAdmin.
            corsPolicyBuilder.WithOrigins(allowedOrigins);
            corsPolicyBuilder.AllowAnyMethod();
            corsPolicyBuilder.AllowAnyHeader();
        });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors();

app.MapHealthChecks("/health");

app.MapReverseProxy();

app.Run();
