// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace EricksonLopez.Transaction.Oracle.Tests;

public sealed class OracleConnectionFactoryTests
{
    private const string TestConnectionString = "Data Source=localhost:1521/XEPDB1;User Id=system;Password=SecretPassword123!;";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenConnectionStringNullOrWhitespace_ShouldThrowArgumentException(string? connStr)
    {
        Action act = () => _ = new OracleConnectionFactory(connStr!);
        act.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }

    [Fact]
    public void CreateConnection_ShouldReturnConfiguredOracleConnection()
    {
        var factory = new OracleConnectionFactory(TestConnectionString);
        using var conn = factory.CreateConnection();

        conn.Should().NotBeNull();
        conn.Should().BeOfType<OracleConnection>();
        conn.ConnectionString.Should().Be(TestConnectionString);
    }

    [Fact]
    public async Task CreateConnectionAsync_ShouldAttemptConnection()
    {
        var factory = new OracleConnectionFactory(TestConnectionString);

        Func<Task> act = async () =>
        {
            using var conn = await factory.CreateConnectionAsync(CancellationToken.None);
        };

        // Without a real Oracle server running, it will throw OracleException trying to connect
        await act.Should().ThrowAsync<OracleException>();
    }
}
