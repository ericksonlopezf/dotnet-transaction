// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Dapper;
using EricksonLopez.Transaction.Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EricksonLopez.Transaction.Dapper.Tests;

public sealed class TransactionDapperExtensionsTests
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TransactionDapperExtensionsTests()
    {
        _connectionFactory = new DelegateDbConnectionFactory(async ct =>
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            await conn.OpenAsync(ct);
            return conn;
        });
    }

    private sealed record TestUser(long Id, string Name, string Email);
    private sealed record TestOrder(long Id, double Amount);

    [Fact]
    public void AsCommand_WhenContextIsNull_ShouldThrowArgumentNullException()
    {
        ITransactionContext context = null!;
        Action act = () => context.AsCommand("SELECT 1;");
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AsCommand_WhenCommandTextIsNullOrEmptyOrWhitespace_ShouldThrowArgumentException(string? sql)
    {
        var manager = new TransactionManager(_connectionFactory);
        await manager.ExecuteAsync(async context =>
        {
            Action act = () => context.AsCommand(sql!);
            act.Should().Throw<ArgumentException>().WithParameterName("commandText");
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AsCommand_ShouldBindAllPropertiesCorrectly()
    {
        var manager = new TransactionManager(_connectionFactory);
        await manager.ExecuteAsync(async context =>
        {
            using var cts = new CancellationTokenSource();
            var cmd = context.AsCommand(
                "SELECT @val;",
                new { val = 42 },
                CommandType.Text,
                CommandFlags.None,
                commandTimeout: 15,
                cancellationToken: cts.Token);

            cmd.CommandText.Should().Be("SELECT @val;");
            cmd.Parameters.Should().NotBeNull();
            cmd.Transaction.Should().BeSameAs(context.Transaction);
            cmd.CommandTimeout.Should().Be(15);
            cmd.CommandType.Should().Be(CommandType.Text);
            cmd.Flags.Should().Be(CommandFlags.None);
            cmd.CancellationToken.CanBeCanceled.Should().BeTrue();

            var defaultCmd = context.AsCommand("SELECT 1;", cancellationToken: CancellationToken.None);
            defaultCmd.CancellationToken.Should().Be(context.CancellationToken);
            defaultCmd.Flags.Should().Be(CommandFlags.Buffered);
            defaultCmd.CommandTimeout.Should().BeNull();
            defaultCmd.CommandType.Should().Be(CommandType.Text);

            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task DapperExtensions_ShouldExecuteAndQueryCorrectly()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async context =>
        {
            using var cts = new CancellationTokenSource();

            await context.ExecuteAsync(
                "CREATE TABLE test_users (id INT PRIMARY KEY, name TEXT, email TEXT);",
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            int inserted = await context.ExecuteAsync(
                "INSERT INTO test_users (id, name, email) VALUES (@id, @name, @email);",
                new { id = 1, name = "Alice", email = "alice@example.com" },
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            inserted.Should().Be(1);

            IEnumerable<TestUser> users = await context.QueryAsync<TestUser>(
                "SELECT id, name, email FROM test_users WHERE id = @id;",
                new { id = 1 },
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            List<TestUser> list = users.ToList();
            list.Should().HaveCount(1);
            list[0].Name.Should().Be("Alice");
            list[0].Email.Should().Be("alice@example.com");

            TestUser single = await context.QuerySingleAsync<TestUser>(
                "SELECT id, name, email FROM test_users WHERE id = @id;",
                new { id = 1 },
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            single.Name.Should().Be("Alice");

            TestUser? singleOrDefault = await context.QuerySingleOrDefaultAsync<TestUser>(
                "SELECT id, name, email FROM test_users WHERE id = @id;",
                new { id = 1 },
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            singleOrDefault.Should().NotBeNull();
            singleOrDefault!.Name.Should().Be("Alice");

            TestUser? notFoundSingle = await context.QuerySingleOrDefaultAsync<TestUser>(
                "SELECT id, name, email FROM test_users WHERE id = @id;",
                new { id = 999 });
            notFoundSingle.Should().BeNull();

            TestUser first = await context.QueryFirstAsync<TestUser>(
                "SELECT id, name, email FROM test_users WHERE id = @id;",
                new { id = 1 },
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            first.Name.Should().Be("Alice");

            TestUser? firstOrDefault = await context.QueryFirstOrDefaultAsync<TestUser>(
                "SELECT id, name, email FROM test_users WHERE id = @id;",
                new { id = 1 },
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            firstOrDefault.Should().NotBeNull();
            firstOrDefault!.Name.Should().Be("Alice");

            TestUser? notFoundFirst = await context.QueryFirstOrDefaultAsync<TestUser>(
                "SELECT id, name, email FROM test_users WHERE id = @id;",
                new { id = 999 });
            notFoundFirst.Should().BeNull();

            int count = await context.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM test_users;",
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            count.Should().Be(1);

            string? emptyScalar = await context.ExecuteScalarAsync<string>(
                "SELECT name FROM test_users WHERE id = 999;");
            emptyScalar.Should().BeNull();
        });
    }

    [Fact]
    public async Task QueryMultipleAsync_ShouldReadMultipleResultSets()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async context =>
        {
            using var cts = new CancellationTokenSource();

            await context.ExecuteAsync("CREATE TABLE users (id INT, name TEXT, email TEXT); CREATE TABLE orders (id INT, amount REAL);");
            await context.ExecuteAsync("INSERT INTO users VALUES (1, 'Alice', 'alice@example.com'); INSERT INTO orders VALUES (10, 99.5);");

            using SqlMapper.GridReader grid = await context.QueryMultipleAsync(
                "SELECT id, name, email FROM users; SELECT id, amount FROM orders;",
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            IEnumerable<TestUser> users = await grid.ReadAsync<TestUser>();
            IEnumerable<TestOrder> orders = await grid.ReadAsync<TestOrder>();

            users.Single().Name.Should().Be("Alice");
            users.Single().Email.Should().Be("alice@example.com");
            orders.Single().Amount.Should().Be(99.5);
        });
    }

    [Fact]
    public async Task ExecuteReaderAsync_ShouldReturnDataReader()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async context =>
        {
            using var cts = new CancellationTokenSource();

            await context.ExecuteAsync("CREATE TABLE items (id INT, name TEXT); INSERT INTO items VALUES (1, 'Book');");

            using IDataReader reader = await context.ExecuteReaderAsync(
                "SELECT id, name FROM items;",
                commandTimeout: 30,
                commandType: CommandType.Text,
                cancellationToken: cts.Token);

            reader.Read().Should().BeTrue();
            reader.GetInt32(0).Should().Be(1);
            reader.GetString(1).Should().Be("Book");
        });
    }
}
