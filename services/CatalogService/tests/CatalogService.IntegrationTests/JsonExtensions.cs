using System.Text.Json;

namespace CatalogService.IntegrationTests;

// Samme som src/ApplicationCore/Extensions/JsonExtensions.cs, så testene ikke afhænger af monolitten.
public static class JsonExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static T? FromJson<T>(this string json) =>
        JsonSerializer.Deserialize<T>(json, _jsonOptions);
}
