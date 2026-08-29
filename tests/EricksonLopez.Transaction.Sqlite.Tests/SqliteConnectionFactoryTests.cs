// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EricksonLopez.Transaction.Sqlite.Tests;

public sealed class SqliteConnectionFactoryTests
{
    private const string TestConnectionString = "Data Source=:memory:";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenConnectionStringNullOrWhitespace_ShouldThrowArgumentException(string? connStr)
    {
        Action act = () => _ = new SqliteConnectionFactory(connStr!);
        act.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }

    [Fact]
    public void CreateConnection_ShouldReturnConfiguredSqliteConnection()
    {
        var factory = new SqliteConnectionFactory(TestConnectionString);
        using var conn = factory.CreateConnection();

        conn.Should().NotBeNull();
        conn.Should().BeOfType<SqliteConnection>();
        conn.ConnectionString.Should().Be(TestConnectionString);
    }

    [Fact]
    public async Task CreateConnectionAsync_ShouldOpenConnection()
    {
        var factory = new SqliteConnectionFactory(TestConnectionString);
        await using var conn = await factory.CreateConnectionAsync(CancellationToken.None);

        conn.Should().NotBeNull();
        conn.State.Should().Be(ConnectionState.Open);
    }
}
