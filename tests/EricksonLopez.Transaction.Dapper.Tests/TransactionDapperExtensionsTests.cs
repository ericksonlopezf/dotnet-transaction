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

    [Fact]
    public async Task DapperMethods_WhenContextIsNull_ShouldThrowArgumentNullException()
    {
        ITransactionContext context = null!;
        Func<Task> act1 = () => context.ExecuteAsync("SELECT 1;");
        Func<Task> act2 = () => context.QueryAsync<int>("SELECT 1;");
        Func<Task> act3 = () => context.QuerySingleOrDefaultAsync<int>("SELECT 1;");
        Func<Task> act4 = () => context.QueryFirstOrDefaultAsync<int>("SELECT 1;");
        Func<Task> act5 = () => context.ExecuteScalarAsync<int>("SELECT 1;");
        Func<Task> act6 = () => context.QuerySingleAsync<int>("SELECT 1;");
        Func<Task> act7 = () => context.QueryFirstAsync<int>("SELECT 1;");

        await act1.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        await act2.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        await act3.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        await act4.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        await act5.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        await act6.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
        await act7.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    private sealed class TestDapperContext : ITransactionContext
    {
        public Guid TransactionId { get; set; } = Guid.NewGuid();
        public TransactionState State { get; set; } = TransactionState.Active;
        public TransactionIsolationLevel IsolationLevel { get; set; } = TransactionIsolationLevel.ReadCommitted;
        public System.Data.Common.DbConnection Connection { get; set; } = null!;
        public System.Data.Common.DbTransaction Transaction { get; set; } = null!;
        public CancellationToken CancellationToken { get; set; }
        public IReadOnlyList<ITransactionEnlistment> Enlistments => [];
        public bool IsRollbackOnly { get; set; }
        public void SetRollbackOnly(string reason) => IsRollbackOnly = true;
        public void Enlist(ITransactionEnlistment enlistment) { }
        public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void AsCommand_CancellationTokenResolution_CoversAllBranches()
    {
        using var ctxCts = new CancellationTokenSource();
        var ctxWithCancel = new TestDapperContext { CancellationToken = ctxCts.Token };

        // 1. cancellationToken is default / None
        var cmd1 = ctxWithCancel.AsCommand("SELECT 1;", cancellationToken: CancellationToken.None);
        cmd1.CancellationToken.Should().Be(ctxCts.Token);

        // 2. cancellationToken is same as context.CancellationToken
        var cmd2 = ctxWithCancel.AsCommand("SELECT 1;", cancellationToken: ctxCts.Token);
        cmd2.CancellationToken.Should().Be(ctxCts.Token);

        // 3. context.CancellationToken cannot be canceled, but caller can
        var ctxNoCancel = new TestDapperContext { CancellationToken = CancellationToken.None };
        using var callerCts = new CancellationTokenSource();
        var cmd3 = ctxNoCancel.AsCommand("SELECT 1;", cancellationToken: callerCts.Token);
        cmd3.CancellationToken.Should().Be(callerCts.Token);

        // 4. both can be canceled
        var cmd4 = ctxWithCancel.AsCommand("SELECT 1;", cancellationToken: callerCts.Token);
        cmd4.CancellationToken.Should().Be(callerCts.Token);
    }

    [Fact]
    public async Task ResolveToken_ThroughExecuteAsync_CoversAllBranches()
    {
        using var fakeConn = new SqliteConnection("Data Source=:memory:");
        await fakeConn.OpenAsync();

        // 1. cancellationToken cannot be canceled -> returns context.CancellationToken (verify cancellation propagates)
        using var cancelableCtxCts = new CancellationTokenSource();
        cancelableCtxCts.Cancel();
        var ctxPreCanceled = new TestDapperContext
        {
            Connection = fakeConn,
            CancellationToken = cancelableCtxCts.Token
        };
        Func<Task> act1 = () => ctxPreCanceled.ExecuteAsync("SELECT 1;", cancellationToken: CancellationToken.None);
        await act1.Should().ThrowAsync<OperationCanceledException>();

        // 2. cancellationToken is same as context.CancellationToken
        Func<Task> act2 = () => ctxPreCanceled.ExecuteAsync("SELECT 1;", cancellationToken: cancelableCtxCts.Token);
        await act2.Should().ThrowAsync<OperationCanceledException>();

        // 3. context.CancellationToken cannot be canceled, but caller can be canceled
        var ctxNoCancel = new TestDapperContext
        {
            Connection = fakeConn,
            CancellationToken = CancellationToken.None
        };
        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();
        Func<Task> act3 = () => ctxNoCancel.ExecuteAsync("SELECT 1;", cancellationToken: callerCts.Token);
        await act3.Should().ThrowAsync<OperationCanceledException>();

        // 4. both can be canceled -> linked CTS cancels when caller cancels
        using var liveCtxCts = new CancellationTokenSource();
        var liveCtx = new TestDapperContext
        {
            Connection = fakeConn,
            CancellationToken = liveCtxCts.Token
        };
        using var liveCallerCts = new CancellationTokenSource();
        liveCallerCts.Cancel();
        Func<Task> act4 = () => liveCtx.ExecuteAsync("SELECT 1;", cancellationToken: liveCallerCts.Token);
        await act4.Should().ThrowAsync<OperationCanceledException>();

        // 5. both can be canceled -> linked CTS cancels when context cancels
        using var liveCtxCts2 = new CancellationTokenSource();
        liveCtxCts2.Cancel();
        var liveCtx2 = new TestDapperContext
        {
            Connection = fakeConn,
            CancellationToken = liveCtxCts2.Token
        };
        using var liveCallerCts2 = new CancellationTokenSource();
        Func<Task> act5 = () => liveCtx2.ExecuteAsync("SELECT 1;", cancellationToken: liveCallerCts2.Token);
        await act5.Should().ThrowAsync<OperationCanceledException>();

        // 6. successful execution with uncancelled linked tokens
        using var okCtxCts = new CancellationTokenSource();
        var okCtx = new TestDapperContext
        {
            Connection = fakeConn,
            CancellationToken = okCtxCts.Token
        };
        using var okCallerCts = new CancellationTokenSource();
        int r = await okCtx.ExecuteAsync("SELECT 1;", cancellationToken: okCallerCts.Token);
        r.Should().Be(-1);

        // 7. successful execution with CancellationToken.None when context has active cancelable token
        int r2 = await okCtx.ExecuteAsync("SELECT 1;", cancellationToken: CancellationToken.None);
        r2.Should().Be(-1);
    }

    [Fact]
    public void ResolveToken_DirectInvocations_CoversBranchDecisions()
    {
        using var fakeConn = new SqliteConnection("Data Source=:memory:");
        using var ctxCts = new CancellationTokenSource();
        var ctxWithCancel = new TestDapperContext
        {
            Connection = fakeConn,
            CancellationToken = ctxCts.Token
        };
        var ctxNoCancel = new TestDapperContext
        {
            Connection = fakeConn,
            CancellationToken = CancellationToken.None
        };
        using var callerCts = new CancellationTokenSource();

        // 1. cancellationToken cannot be canceled -> returns context.CancellationToken, null
        var (t1, cts1) = TransactionDapperExtensions.ResolveToken(ctxWithCancel, CancellationToken.None);
        t1.Should().Be(ctxWithCancel.CancellationToken);
        cts1.Should().BeNull();

        // 2. cancellationToken is same as context.CancellationToken -> returns context.CancellationToken, null
        var (t2, cts2) = TransactionDapperExtensions.ResolveToken(ctxWithCancel, ctxCts.Token);
        t2.Should().Be(ctxWithCancel.CancellationToken);
        cts2.Should().BeNull();

        // 3. context.CancellationToken cannot be canceled -> returns cancellationToken, null
        var (t3, cts3) = TransactionDapperExtensions.ResolveToken(ctxNoCancel, callerCts.Token);
        t3.Should().Be(callerCts.Token);
        cts3.Should().BeNull();

        // 4. both can be canceled -> returns linked token, non-null cts
        var (t4, cts4) = TransactionDapperExtensions.ResolveToken(ctxWithCancel, callerCts.Token);
        cts4.Should().NotBeNull();
        t4.Should().Be(cts4!.Token);
        cts4.Dispose();
    }
}
