using DentaCore.Infrastructure.Identity;
using DentaCore.Shared.Helpers;
using Xunit;

namespace DentaCore.UnitTests;

public class CoreLogicTests
{
    [Fact]
    public void ReceiptNumberGenerator_GeneratesCorrectFormat()
    {
        var receiptNum = ReceiptNumberGenerator.Generate();
        Assert.NotNull(receiptNum);
        Assert.StartsWith("RCPT-", receiptNum);
        var parts = receiptNum.Split('-');
        Assert.Equal(3, parts.Length);
        Assert.Equal(8, parts[1].Length); // yyyyMMdd
    }

    [Fact]
    public void PasswordHasher_HashesAndVerifiesPassword()
    {
        var hasher = new PasswordHasher();
        var password = "Doctor@SecretPassword123!";
        var hash = hasher.HashPassword(password);

        Assert.NotEmpty(hash);
        Assert.Contains(".", hash);

        var isValid = hasher.VerifyPassword(password, hash);
        Assert.True(isValid);

        var isInvalid = hasher.VerifyPassword("WrongPassword123!", hash);
        Assert.False(isInvalid);
    }
}
