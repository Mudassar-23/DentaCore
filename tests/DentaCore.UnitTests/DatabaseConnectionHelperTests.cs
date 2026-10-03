using DentaCore.Infrastructure.Persistence;
using Xunit;

namespace DentaCore.UnitTests;

public class DatabaseConnectionHelperTests
{
    [Fact]
    public void ToPostgreSqlConnectionString_ConvertsUriProperly()
    {
        var uri = "postgresql://username:password@localhost:5432/aidocs";
        var result = DatabaseConnectionHelper.ToPostgreSqlConnectionString(uri);

        Assert.Contains("Host=localhost", result);
        Assert.Contains("Port=5432", result);
        Assert.Contains("Database=aidocs", result);
        Assert.Contains("Username=username", result);
        Assert.Contains("Password=password", result);
    }

    [Fact]
    public void ToPostgreSqlConnectionString_HandlesStandardAdoNetPassThrough()
    {
        var standard = "Host=myhost;Port=5432;Database=mydb;Username=myuser;Password=mypass;";
        var result = DatabaseConnectionHelper.ToPostgreSqlConnectionString(standard);

        Assert.Equal(standard, result);
    }

    [Fact]
    public void NormalizeSqliteConnectionString_ConvertsUriAndPath()
    {
        var uri = "sqlite:///./data/aiNEWkkk.db";
        var result = DatabaseConnectionHelper.NormalizeSqliteConnectionString(uri);

        Assert.Equal("Data Source=./data/aiNEWkkk.db", result);

        var plain = "Data Source=data/aiNEWkkk.db";
        var resultPlain = DatabaseConnectionHelper.NormalizeSqliteConnectionString(plain);
        Assert.Equal("Data Source=data/aiNEWkkk.db", resultPlain);
    }
}
