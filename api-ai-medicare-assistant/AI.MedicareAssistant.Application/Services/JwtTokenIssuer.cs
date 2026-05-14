using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Documents;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Application.Services;

public class JwtTokenIssuer : IJwtTokenIssuer
{
    private readonly IConfiguration _config;

    public JwtTokenIssuer(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresAt) Issue(UserDocument user, Guid? actingAs = null, TimeSpan? ttl = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var lifetime = ttl ?? TimeSpan.FromHours(GetTokenExpiryHours());
        var expiresAt = DateTime.UtcNow.Add(lifetime);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("mustChangePassword", user.MustChangePassword ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.FpgId.HasValue)
            claims.Add(new Claim("fpgId", user.FpgId.Value.ToString()));
        if (user.FpId.HasValue)
            claims.Add(new Claim("fpId", user.FpId.Value.ToString()));
        if (actingAs.HasValue)
            claims.Add(new Claim("actingAs", actingAs.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private int GetTokenExpiryHours() =>
        int.TryParse(_config["Jwt:ExpiryHours"], out var hours) ? hours : 24;
}
