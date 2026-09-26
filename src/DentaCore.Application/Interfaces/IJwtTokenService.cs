using DentaCore.Domain.Entities;

namespace DentaCore.Application.Interfaces;

public interface IJwtTokenService
{
    (string AccessToken, string RefreshToken, DateTime AccessExpiresAt, DateTime RefreshExpiresAt) GenerateTokens(User user, Guid? profileId);
}
