using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Spydersoft.AuditSeeder;

/// <summary>
/// Generates a JWT signed with the same symmetric test key the AuditApi expects
/// when running in the Testing environment. Mirrors
/// <c>Spydersoft.PitStop.DataSeeder.TokenGenerator</c>.
/// </summary>
public static class TokenGenerator
{
    public const string TestUserId = "seeder-test-user";

    public static string Generate(string base64Key)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(base64Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: [
                new Claim(JwtRegisteredClaimNames.Sub, TestUserId),
                new Claim("scope", "audit:read"),
            ],
            expires: DateTime.UtcNow.AddDays(365),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
