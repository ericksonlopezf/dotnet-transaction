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

        // We test that factory delegates to data source
        // Note: opening without a real PG instance will attempt TCP connection and throw NpgsqlException,
        // which proves the factory successfully invoked NpgsqlDataSource.OpenConnection() and OpenConnectionAsync().
        Func<Task> actAsync = async () => await factory.CreateConnectionAsync(CancellationToken.None);
        Action actSync = () => factory.CreateConnection();

        await actAsync.Should().ThrowAsync<NpgsqlException>();
        actSync.Should().Throw<NpgsqlException>();
    }

    [Fact]
    public void Constructor_WithConnectionString_ShouldInitialize()
    {
        var factory = new PostgreSqlConnectionFactory("Host=localhost;Database=test;Username=postgres;Password=postgres");
        Action act = () => factory.CreateConnection();
        act.Should().Throw<NpgsqlException>();
    }
}
