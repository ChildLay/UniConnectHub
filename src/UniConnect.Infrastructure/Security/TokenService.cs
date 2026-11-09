using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using UniConnect.Application.Common;
using UniConnect.Domain.Users;

namespace UniConnect.Infrastructure.Security;

public sealed class TokenService(IConfiguration configuration, IClock clock) : ITokenService
{
    public string CreateAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        var keyText = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(keyText) || Encoding.UTF8.GetByteCount(keyText) < 32)
            throw new InvalidOperationException("JWT signing key must be supplied through secrets or environment configuration and contain at least 32 bytes.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("journey_stage", user.JourneyStage.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.Add(new Claim(ClaimTypes.Role, user.Role.ToString()));
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            notBefore: clock.UtcNow, expires: clock.UtcNow.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 15)), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateSecureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
