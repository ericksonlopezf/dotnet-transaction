// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace EricksonLopez.Transaction.SqlServer.Tests;

public sealed class SqlServerConnectionFactoryTests
{
    private const string TestConnectionString = "Server=tcp:localhost,1433;Database=TestDb;User Id=sa;Password=SecretPassword123!;Encrypt=True;TrustServerCertificate=True;";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenConnectionStringNullOrWhitespace_ShouldThrowArgumentException(string? connStr)
    {
        Action act = () => _ = new SqlServerConnectionFactory(connStr!);
        act.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }

    [Fact]
    public void CreateConnection_ShouldReturnConfiguredSqlConnection()
    {
        var factory = new SqlServerConnectionFactory(TestConnectionString);
        using var conn = factory.CreateConnection();

        conn.Should().NotBeNull();
        conn.Should().BeOfType<SqlConnection>();
        conn.ConnectionString.Should().Be(TestConnectionString);
    }

    [Fact]
    public async Task CreateConnectionAsync_ShouldAttemptConnection()
    {
        var factory = new SqlServerConnectionFactory(TestConnectionString);

        Func<Task> act = async () =>
        {
            using var conn = await factory.CreateConnectionAsync(CancellationToken.None);
        };

        // Without a real SQL server running, it will throw SqlException trying to connect
        await act.Should().ThrowAsync<SqlException>();
    }
}
