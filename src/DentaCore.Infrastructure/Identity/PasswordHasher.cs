using System.Security.Cryptography;
using DentaCore.Application.Interfaces;

namespace DentaCore.Infrastructure.Identity;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32;  // 256 bit
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithm,
            KeySize);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.', 2);
        if (parts.Length != 2)
            return false;

        byte[] salt;
        byte[] hash;

        try
        {
            salt = Convert.FromBase64String(parts[0]);
            hash = Convert.FromBase64String(parts[1]);
        }
        catch
        {
            return false;
        }

        byte[] testHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithm,
            KeySize);

        return CryptographicOperations.FixedTimeEquals(hash, testHash);
    }
}
