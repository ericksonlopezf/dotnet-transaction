// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Npgsql;
using Xunit;

namespace EricksonLopez.Transaction.PostgreSql.Tests;

public sealed class PostgreSqlConnectionFactoryTests
{
    [Fact]
    public void Constructor_WhenDataSourceNull_ShouldThrowArgumentNullException()
    {
        Action act = () => _ = new PostgreSqlConnectionFactory((NpgsqlDataSource)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dataSource");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenConnectionStringNullOrWhitespace_ShouldThrowArgumentException(string? connStr)
    {
        Action act = () => _ = new PostgreSqlConnectionFactory(connStr!);
        act.Should().Throw<ArgumentException>().WithParameterName("connectionString");
    }

    [Fact]
    public async Task CreateConnectionMethods_ShouldReturnNpgsqlConnection()
    {
        var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=test;Username=postgres;Password=postgres");
        var factory = new PostgreSqlConnectionFactory(dataSource);

        // Async method opens connection and throws without a running server
        Func<Task> actAsync = async () => await factory.CreateConnectionAsync(CancellationToken.None);
        await actAsync.Should().ThrowAsync<NpgsqlException>();

        // Sync method creates a new unopened connection conforming to IDbConnectionFactory
        using var conn = factory.CreateConnection();
        conn.Should().BeOfType<NpgsqlConnection>();
        conn.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void Constructor_WithConnectionString_ShouldInitialize()
    {
        var factory = new PostgreSqlConnectionFactory("Host=localhost;Database=test;Username=postgres;Password=postgres");
        using var conn = factory.CreateConnection();
        conn.Should().BeOfType<NpgsqlConnection>();
        conn.State.Should().Be(System.Data.ConnectionState.Closed);
    }
}
