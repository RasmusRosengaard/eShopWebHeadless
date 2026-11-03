using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CatalogService.IntegrationTests;

// Laver tokens på samme måde som PublicApi gør ved login,
// signeret med den JwtSecretKey som CatalogService selv validerer med.
public class ApiTokenHelper
{
    public static string GetAdminUserToken()
    {
        string userName = "admin@microsoft.com";
        string[] roles = { "Administrators" };

        return CreateToken(userName, roles);
    }

    public static string GetNormalUserToken()
    {
        string userName = "demouser@microsoft.com";
        string[] roles = { };

        return CreateToken(userName, roles);
    }

    private static string CreateToken(string userName, string[] roles)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, userName) };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var configuration = ProgramTest.Services.GetRequiredService<IConfiguration>();
        var key = Encoding.ASCII.GetBytes(configuration["JwtSecretKey"]!);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims.ToArray()),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
