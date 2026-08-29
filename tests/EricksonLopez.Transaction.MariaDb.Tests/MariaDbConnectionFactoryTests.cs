// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using MySqlConnector;
using Xunit;

namespace EricksonLopez.Transaction.MariaDb.Tests;

public sealed class MariaDbConnectionFactoryTests
{
    private const string TestConnectionString = "Server=localhost;Port=3306;Database=TestDb;Uid=root;Pwd=SecretPassword123!;";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenConnectionStringNullOrWhitespace_ShouldThrowArgumentException(string? connStr)
    {
        Action act = () => _ = new MariaDbConnectionFactory(connStr!);
        act.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }

    [Fact]
    public void CreateConnection_ShouldReturnConfiguredMySqlConnection()
    {
        var factory = new MariaDbConnectionFactory(TestConnectionString);
        using var conn = factory.CreateConnection();

        conn.Should().NotBeNull();
        conn.Should().BeOfType<MySqlConnection>();
        conn.ConnectionString.Should().Be(TestConnectionString);
    }

    [Fact]
    public async Task CreateConnectionAsync_ShouldAttemptConnection()
    {
        var factory = new MariaDbConnectionFactory(TestConnectionString);

        Func<Task> act = async () =>
        {
            using var conn = await factory.CreateConnectionAsync(CancellationToken.None);
        };

        // Without a real MariaDB server running, it will throw MySqlException trying to connect
        await act.Should().ThrowAsync<MySqlException>();
    }
}
