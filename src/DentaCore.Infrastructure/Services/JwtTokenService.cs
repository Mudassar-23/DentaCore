using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DentaCore.Application.Interfaces;
using DentaCore.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DentaCore.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string AccessToken, string RefreshToken, DateTime AccessExpiresAt, DateTime RefreshExpiresAt) GenerateTokens(
        User user,
        Guid? profileId)
    {
        var secret = _configuration["JWT_SECRET"] ?? _configuration["Jwt:Secret"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!!";
        var issuer = _configuration["JWT_ISSUER"] ?? _configuration["Jwt:Issuer"] ?? "DentaCore";
        var audience = _configuration["JWT_AUDIENCE"] ?? _configuration["Jwt:Audience"] ?? "DentaCoreApp";

        var accessMinutes = int.TryParse(_configuration["JWT_ACCESS_TOKEN_EXPIRY_MINUTES"], out var m) ? m : 60;
        var refreshDays = int.TryParse(_configuration["JWT_REFRESH_TOKEN_EXPIRY_DAYS"], out var d) ? d : 7;

        var accessExpiresAt = DateTime.UtcNow.AddMinutes(accessMinutes);
        var refreshExpiresAt = DateTime.UtcNow.AddDays(refreshDays);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role", user.Role.ToString())
        };

        if (profileId.HasValue)
        {
            claims.Add(new Claim("profileId", profileId.Value.ToString()));
            if (user.Role == Domain.Enums.UserRole.Doctor)
                claims.Add(new Claim("doctorId", profileId.Value.ToString()));
            else if (user.Role == Domain.Enums.UserRole.Patient)
                claims.Add(new Claim("patientId", profileId.Value.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = accessExpiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var refreshToken = Convert.ToBase64String(randomBytes);

        return (accessToken, refreshToken, accessExpiresAt, refreshExpiresAt);
    }
}
