// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class TransactionManagerTests
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TransactionManagerTests()
    {
        _connectionFactory = new DelegateDbConnectionFactory(async ct =>
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            await conn.OpenAsync(ct);
            return conn;
        });
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, EventId Id, string Message)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, eventId, formatter(state, exception)));
        }
    }

    private sealed class AlreadyOpenTrackingConnection : DbConnection
    {
        public int OpenAsyncCallCount { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "TestDb";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenAsyncCallCount++;
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => new NpgsqlFakeTransaction(this);

        protected override DbCommand CreateDbCommand()
            => new NpgsqlFakeCommand();
    }

    private sealed class ClosedTrackingConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        public int OpenAsyncCallCount { get; private set; }
        public int DisposeAsyncCallCount { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "TestDb";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { _state = ConnectionState.Closed; }
        public override void Open() { _state = ConnectionState.Open; }
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenAsyncCallCount++;
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeAsyncCallCount++;
            _state = ConnectionState.Closed;
            return base.DisposeAsync();
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => new NpgsqlFakeTransaction(this);

        protected override DbCommand CreateDbCommand()
            => new NpgsqlFakeCommand();
    }

    private sealed class NpgsqlFakeConnection : DbConnection
    {
        public DbCommand? LastCreatedCommand { get; private set; }
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "postgres";
        public override string DataSource => "localhost";
        public override string ServerVersion => "15.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => new NpgsqlFakeTransaction(this);
        protected override DbCommand CreateDbCommand()
        {
            var cmd = new NpgsqlFakeCommand();
            LastCreatedCommand = cmd;
            return cmd;
        }
    }

    private sealed class NpgsqlFakeTransaction : DbTransaction
    {
        private readonly DbConnection _conn;
        public NpgsqlFakeTransaction(DbConnection conn) => _conn = conn;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _conn;
        public override void Commit() { }
        public override void Rollback() { }
        public override Task SaveAsync(string savepointName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public override Task ReleaseAsync(string savepointName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public override Task RollbackAsync(string savepointName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NpgsqlFakeCommand : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 1;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class MySqlThrowingConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "mysql";
        public override string DataSource => "localhost";
        public override string ServerVersion => "8.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => new NpgsqlFakeTransaction(this);
        protected override DbCommand CreateDbCommand()
            => new MySqlThrowingCommand();
    }

    private sealed class MySqlThrowingCommand : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new InvalidOperationException("MySQL command failure");
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromException<int>(new InvalidOperationException("MySQL command failure"));
    }

    private sealed class MySqlFakeConnection : DbConnection
    {
        public DbCommand? LastCreatedCommand { get; private set; }
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "mysql";
        public override string DataSource => "localhost";
        public override string ServerVersion => "8.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => new NpgsqlFakeTransaction(this);
        protected override DbCommand CreateDbCommand()
        {
            var cmd = new NpgsqlFakeCommand();
            LastCreatedCommand = cmd;
            return cmd;
        }
    }

    private sealed class CustomOtherDbConnection : DbConnection
    {
        public DbCommand? LastCreatedCommand { get; private set; }
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "other";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => new NpgsqlFakeTransaction(this);
        protected override DbCommand CreateDbCommand()
        {
            var cmd = new NpgsqlFakeCommand();
            LastCreatedCommand = cmd;
            return cmd;
        }
    }

    private sealed class ReleaseFailingTransaction : DbTransaction
    {
        private readonly DbConnection _conn;
        public ReleaseFailingTransaction(DbConnection conn) => _conn = conn;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _conn;
        public override void Commit() { }
        public override void Rollback() { }
        public override Task SaveAsync(string savepointName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task ReleaseAsync(string savepointName, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("Release savepoint failed"));
        public override Task RollbackAsync(string savepointName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ReleaseFailingConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "test";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => new ReleaseFailingTransaction(this);
        protected override DbCommand CreateDbCommand()
            => new NpgsqlFakeCommand();
    }

    [Fact]
    public void Constructor_WhenConnectionFactoryNull_ShouldThrowArgumentNullException()
    {
        Action act = () => _ = new TransactionManager(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteAndCommitSuccessfully()
    {
        var manager = new TransactionManager(_connectionFactory);
        bool executed = false;

        await manager.ExecuteAsync(async context =>
        {
            executed = true;
            context.Should().NotBeNull();
            context.State.Should().Be(TransactionState.Active);
            manager.CurrentContext.Should().BeSameAs(context);

            await using var cmd = context.Connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)context.Transaction;
            cmd.CommandText = "CREATE TABLE users (id INT PRIMARY KEY, name TEXT);";
            await cmd.ExecuteNonQueryAsync();
        });

        executed.Should().BeTrue();
        manager.CurrentContext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenInitiallyClosed_ShouldAutoOpenConnectionAndDisposeOnExit()
    {
        var closedConn = new ClosedTrackingConnection();
        var factory = new DelegateDbConnectionFactory(() => closedConn);
        var manager = new TransactionManager(factory);

        await manager.ExecuteAsync(async ctx =>
        {
            ctx.Connection.State.Should().Be(ConnectionState.Open);
            await Task.CompletedTask;
        });

        closedConn.OpenAsyncCallCount.Should().Be(1);
        closedConn.DisposeAsyncCallCount.Should().Be(1);

        // When connection is already open, OpenAsync should not be called
        var openConn = new AlreadyOpenTrackingConnection();
        var openFactory = new DelegateDbConnectionFactory(() => openConn);
        var openManager = new TransactionManager(openFactory);

        await openManager.ExecuteAsync(async ctx =>
        {
            await Task.CompletedTask;
        });
        openConn.OpenAsyncCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_Parameterless_ShouldExecuteAndCommitSuccessfully()
    {
        var manager = new TransactionManager(_connectionFactory);
        bool executed = false;

        await manager.ExecuteAsync(async () =>
        {
            executed = true;
            manager.CurrentContext.Should().NotBeNull();
            manager.CurrentContext!.State.Should().Be(TransactionState.Active);
            await Task.CompletedTask;
        });

        executed.Should().BeTrue();
        manager.CurrentContext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_ShouldReturnResultAndCommit()
    {
        var manager = new TransactionManager(_connectionFactory);

        int result = await manager.ExecuteAsync(async context =>
        {
            await using var cmd = context.Connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)context.Transaction;
            cmd.CommandText = "CREATE TABLE items (id INT); INSERT INTO items VALUES (42);";
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = "SELECT id FROM items LIMIT 1;";
            object? val = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(val, System.Globalization.CultureInfo.InvariantCulture);
        });

        result.Should().Be(42);
        manager.CurrentContext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ParameterlessWithResult_ShouldReturnResultAndCommit()
    {
        var manager = new TransactionManager(_connectionFactory);

        string result = await manager.ExecuteAsync(async () =>
        {
            manager.CurrentContext.Should().NotBeNull();
            await Task.CompletedTask;
            return "computed_value";
        });

        result.Should().Be("computed_value");
        manager.CurrentContext.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeoutThatDoesNotExpire_ShouldCompleteSuccessfully()
    {
        var manager = new TransactionManager(_connectionFactory);
        var options = TransactionOptions.WithTimeout(TimeSpan.FromSeconds(30));

        await manager.ExecuteAsync(async ctx =>
        {
            ctx.Should().NotBeNull();
            await Task.CompletedTask;
        }, options);

        await manager.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
        }, options);

        int res1 = await manager.ExecuteAsync(async ctx =>
        {
            await Task.CompletedTask;
            return 100;
        }, options);

        string res2 = await manager.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
            return "ok";
        }, options);

        res1.Should().Be(100);
        res2.Should().Be("ok");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationThrowsOperationCanceledExceptionDirectly_ShouldNotWrapInTimeoutException()
    {
        var logger = new TestLogger<TransactionManager>();
        var manager = new TransactionManager(_connectionFactory, logger);
        var options = TransactionOptions.WithTimeout(TimeSpan.FromSeconds(30));

        Func<Task> act1 = () => manager.ExecuteAsync(async ctx =>
        {
            await Task.Yield();
            throw new OperationCanceledException("Delegate explicit cancellation");
        }, options);

        Func<Task> act2 = () => manager.ExecuteAsync(async () =>
        {
            await Task.Yield();
            throw new OperationCanceledException("Delegate explicit cancellation");
        }, options);

        Func<Task> act3 = () => manager.ExecuteAsync<int>(async ctx =>
        {
            await Task.Yield();
            throw new OperationCanceledException("Delegate explicit cancellation");
        }, options);

        Func<Task> act4 = () => manager.ExecuteAsync<int>(async () =>
        {
            await Task.Yield();
            throw new OperationCanceledException("Delegate explicit cancellation");
        }, options);

        var ex1 = await act1.Should().ThrowAsync<OperationCanceledException>();
        ex1.Which.Should().NotBeOfType<TransactionTimeoutException>();
        var ex2 = await act2.Should().ThrowAsync<OperationCanceledException>();
        ex2.Which.Should().NotBeOfType<TransactionTimeoutException>();
        var ex3 = await act3.Should().ThrowAsync<OperationCanceledException>();
        ex3.Which.Should().NotBeOfType<TransactionTimeoutException>();
        var ex4 = await act4.Should().ThrowAsync<OperationCanceledException>();
        ex4.Which.Should().NotBeOfType<TransactionTimeoutException>();

        logger.Logs.Should().NotContain(l => l.Id.Id == 2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerTokenCancelledEvenWithTimeout_ShouldRethrowOperationCanceledExceptionAndNotLogTimeout()
    {
        var logger = new TestLogger<TransactionManager>();
        var manager = new TransactionManager(_connectionFactory, logger);
        var options = TransactionOptions.WithTimeout(TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act1 = () => manager.ExecuteAsync(async ctx => { await Task.CompletedTask; }, options, cts.Token);
        Func<Task> act2 = () => manager.ExecuteAsync(async () => { await Task.CompletedTask; }, options, cts.Token);
        Func<Task> act3 = () => manager.ExecuteAsync(async ctx => { await Task.CompletedTask; return 1; }, options, cts.Token);
        Func<Task> act4 = () => manager.ExecuteAsync(async () => { await Task.CompletedTask; return 1; }, options, cts.Token);

        await act1.Should().ThrowAsync<OperationCanceledException>();
        await act2.Should().ThrowAsync<OperationCanceledException>();
        await act3.Should().ThrowAsync<OperationCanceledException>();
        await act4.Should().ThrowAsync<OperationCanceledException>();

        logger.Logs.Should().NotContain(l => l.Id.Id == 2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceptionOccurs_ShouldRollbackAndPropagate()
    {
        var manager = new TransactionManager(_connectionFactory);

        Func<Task> act = async () =>
        {
            await manager.ExecuteAsync(async context =>
            {
                await using var cmd = context.Connection.CreateCommand();
                cmd.Transaction = (SqliteTransaction)context.Transaction;
                cmd.CommandText = "CREATE TABLE users (id INT PRIMARY KEY, name TEXT);";
                await cmd.ExecuteNonQueryAsync();

                throw new InvalidOperationException("Business failure occurred");
            });
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Business failure occurred");
        manager.CurrentContext.Should().BeNull();
    }

    [Fact]
    public async Task NestedExecuteAsync_DefaultSavepoint_ShouldCreateSavepointWithAutoGeneratedName()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async outerContext =>
        {
            await using var cmd1 = outerContext.Connection.CreateCommand();
            cmd1.Transaction = (SqliteTransaction)outerContext.Transaction;
            cmd1.CommandText = "CREATE TABLE records (id INT PRIMARY KEY, val TEXT);";
            await cmd1.ExecuteNonQueryAsync();

            cmd1.CommandText = "INSERT INTO records VALUES (1, 'Outer');";
            await cmd1.ExecuteNonQueryAsync();

            await manager.ExecuteAsync(async innerContext =>
            {
                innerContext.Should().BeSameAs(outerContext);
                await using var cmd2 = innerContext.Connection.CreateCommand();
                cmd2.Transaction = (SqliteTransaction)innerContext.Transaction;
                cmd2.CommandText = "INSERT INTO records VALUES (2, 'Inner');";
                await cmd2.ExecuteNonQueryAsync();
            });

            cmd1.CommandText = "SELECT COUNT(*) FROM records;";
            long count = (long)(await cmd1.ExecuteScalarAsync())!;
            count.Should().Be(2);
        });

        manager.CurrentContext.Should().BeNull();
    }

    [Fact]
    public async Task NestedExecuteAsync_NamedSavepoint_ShouldUseProvidedName()
    {
        var failingConn = new ReleaseFailingConnection();
        var factory = new DelegateDbConnectionFactory(() => failingConn);
        var manager = new TransactionManager(factory);

        await manager.ExecuteAsync(async outerContext =>
        {
            var options = new TransactionOptions
            {
                NestedBehavior = NestedTransactionBehavior.UseSavepoint,
                TransactionName = "custom_savepoint_name"
            };

            Func<Task> act = () => manager.ExecuteAsync(async innerContext =>
            {
                await Task.CompletedTask;
            }, options);

            var ex = await act.Should().ThrowAsync<TransactionCommitException>();
            ex.Which.Message.Should().Contain("custom_savepoint_name");
        });
    }

    [Fact]
    public async Task NestedExecuteAsync_FallbackBehavior_ShouldCreateSavepoint()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async outerContext =>
        {
            var fallbackOptions = new TransactionOptions { NestedBehavior = (NestedTransactionBehavior)999 };
            await manager.ExecuteAsync(async innerContext =>
            {
                innerContext.Should().BeSameAs(outerContext);
                await Task.CompletedTask;
            }, fallbackOptions);
        });
    }

    [Fact]
    public async Task NestedExecuteAsync_JoinExisting_ShouldParticipateWithoutSavepoint()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async outerContext =>
        {
            var joinOptions = new TransactionOptions { NestedBehavior = NestedTransactionBehavior.JoinExisting };
            await manager.ExecuteAsync(async innerContext =>
            {
                innerContext.Should().BeSameAs(outerContext);
                await Task.CompletedTask;
            }, joinOptions);
        });
    }

    [Fact]
    public async Task NestedExecuteAsync_RequireNew_ShouldCreateSeparatePhysicalTransaction()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async outerContext =>
        {
            var requireNewOptions = new TransactionOptions { NestedBehavior = NestedTransactionBehavior.RequireNew };
            await manager.ExecuteAsync(async innerContext =>
            {
                innerContext.Should().NotBeSameAs(outerContext);
                innerContext.TransactionId.Should().NotBe(outerContext.TransactionId);
                await Task.CompletedTask;
            }, requireNewOptions);
        });
    }

    [Fact]
    public async Task Suppress_WhenAmbientExists_ShouldSuspendAmbientAndRestoreOnDispose()
    {
        var logger = new TestLogger<TransactionManager>();
        var manager = new TransactionManager(_connectionFactory, logger);

        await manager.ExecuteAsync(async outerContext =>
        {
            manager.CurrentContext.Should().BeSameAs(outerContext);

            var suppressOptions = new TransactionOptions { NestedBehavior = NestedTransactionBehavior.Suppress };
            await using (ITransaction suppressed = await manager.BeginAsync(suppressOptions))
            {
                suppressed.State.Should().Be(TransactionState.Active);
                manager.CurrentContext.Should().BeNull();

                var actContext = () => _ = suppressed.Context;
                actContext.Should().Throw<InvalidOperationException>()
                    .WithMessage("*suppressed*");

                var actSp = async () => await suppressed.CreateSavepointAsync("sp1");
                await actSp.Should().ThrowAsync<InvalidOperationException>();

                await suppressed.CommitAsync();
            }

            manager.CurrentContext.Should().BeSameAs(outerContext);
        });

        manager.CurrentContext.Should().BeNull();
        logger.Logs.Should().Contain(l => l.Level == LogLevel.Debug && l.Id.Id == 1);
    }

    [Fact]
    public async Task Suppress_WithParameterlessExecuteAsync_ShouldExecuteWithoutAmbientContext()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async outerContext =>
        {
            manager.CurrentContext.Should().BeSameAs(outerContext);

            var suppressOptions = new TransactionOptions { NestedBehavior = NestedTransactionBehavior.Suppress };
            await manager.ExecuteAsync(async () =>
            {
                manager.CurrentContext.Should().BeNull();
                await Task.CompletedTask;
            }, suppressOptions);

            manager.CurrentContext.Should().BeSameAs(outerContext);
        });
    }

    [Fact]
    public async Task Suppress_WithParameterlessResultExecuteAsync_ShouldExecuteAndReturn()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async outerContext =>
        {
            var suppressOptions = new TransactionOptions { NestedBehavior = NestedTransactionBehavior.Suppress };
            string res = await manager.ExecuteAsync(async () =>
            {
                manager.CurrentContext.Should().BeNull();
                await Task.CompletedTask;
                return "suppressed_result";
            }, suppressOptions);

            res.Should().Be("suppressed_result");
        });
    }

    [Fact]
    public async Task Suppress_WhenPassingContextDelegate_ShouldThrowInvalidOperationException()
    {
        var manager = new TransactionManager(_connectionFactory);

        await manager.ExecuteAsync(async outerContext =>
        {
            var suppressOptions = new TransactionOptions { NestedBehavior = NestedTransactionBehavior.Suppress };
            var act1 = async () => await manager.ExecuteAsync(ctx => Task.CompletedTask, suppressOptions);
            var act2 = async () => await manager.ExecuteAsync(ctx => Task.FromResult(1), suppressOptions);

            await act1.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Cannot pass an ITransactionContext operation to a suppressed transaction scope. Use ExecuteAsync(Func<Task>) instead.");
            await act2.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Cannot pass an ITransactionContext operation to a suppressed transaction scope. Use ExecuteAsync(Func<Task<TResult>>) instead.");
        });
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeoutExpires_ShouldThrowTransactionTimeoutExceptionAndLog()
    {
        var logger = new TestLogger<TransactionManager>();
        var manager = new TransactionManager(_connectionFactory, logger);
        var options = TransactionOptions.WithTimeout(TimeSpan.FromMilliseconds(50));

        var act1 = async () =>
        {
            await manager.ExecuteAsync(async context =>
            {
                await Task.Delay(200, context.CancellationToken);
            }, options);
        };

        var act2 = async () =>
        {
            await manager.ExecuteAsync(async () =>
            {
                await Task.Delay(200);
            }, options);
        };

        var act3 = async () =>
        {
            await manager.ExecuteAsync(async context =>
            {
                await Task.Delay(200, context.CancellationToken);
                return 1;
            }, options);
        };

        var act4 = async () =>
        {
            await manager.ExecuteAsync(async () =>
            {
                await Task.Delay(200);
                return 1;
            }, options);
        };

        var ex1 = await act1.Should().ThrowAsync<TransactionTimeoutException>();
        ex1.Which.Timeout.Should().Be(TimeSpan.FromMilliseconds(50));
        var ex2 = await act2.Should().ThrowAsync<TransactionTimeoutException>();
        ex2.Which.Timeout.Should().Be(TimeSpan.FromMilliseconds(50));
        var ex3 = await act3.Should().ThrowAsync<TransactionTimeoutException>();
        ex3.Which.Timeout.Should().Be(TimeSpan.FromMilliseconds(50));
        var ex4 = await act4.Should().ThrowAsync<TransactionTimeoutException>();
        ex4.Which.Timeout.Should().Be(TimeSpan.FromMilliseconds(50));

        logger.Logs.FindAll(l => l.Level == LogLevel.Warning && l.Id.Id == 2).Count.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteAsync_NullOperations_ShouldThrowArgumentNullException()
    {
        var manager = new TransactionManager(_connectionFactory);

        Func<Task> act1 = () => manager.ExecuteAsync((Func<ITransactionContext, Task>)null!);
        Func<Task> act2 = () => manager.ExecuteAsync((Func<Task>)null!);
        Func<Task> act3 = () => manager.ExecuteAsync((Func<ITransactionContext, Task<int>>)null!);
        Func<Task> act4 = () => manager.ExecuteAsync((Func<Task<int>>)null!);

        await act1.Should().ThrowAsync<ArgumentNullException>();
        await act2.Should().ThrowAsync<ArgumentNullException>();
        await act3.Should().ThrowAsync<ArgumentNullException>();
        await act4.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReadOnlyOption_OnNpgsqlAndMySql_ShouldApplySetTransactionReadOnly()
    {
        var npgsqlConn = new NpgsqlFakeConnection();
        var npgsqlFactory = new DelegateDbConnectionFactory(() => npgsqlConn);
        var npgsqlManager = new TransactionManager(npgsqlFactory);

        await npgsqlManager.ExecuteAsync(async ctx =>
        {
            await Task.CompletedTask;
        }, TransactionOptions.ReadOnlyMode);

        npgsqlConn.LastCreatedCommand.Should().NotBeNull();
        npgsqlConn.LastCreatedCommand!.CommandText.Should().Be("SET TRANSACTION READ ONLY;");

        var mysqlConn = new MySqlFakeConnection();
        var mysqlFactory = new DelegateDbConnectionFactory(() => mysqlConn);
        var mysqlManager = new TransactionManager(mysqlFactory);

        await mysqlManager.ExecuteAsync(async ctx =>
        {
            await Task.CompletedTask;
        }, TransactionOptions.ReadOnlyMode);

        mysqlConn.LastCreatedCommand.Should().NotBeNull();
        mysqlConn.LastCreatedCommand!.CommandText.Should().Be("SET TRANSACTION READ ONLY;");
    }

    [Fact]
    public async Task ReadOnlyOption_OnCustomOtherConnection_ShouldNotExecuteCommand()
    {
        var otherConn = new CustomOtherDbConnection();
        var otherFactory = new DelegateDbConnectionFactory(() => otherConn);
        var otherManager = new TransactionManager(otherFactory);

        await otherManager.ExecuteAsync(async ctx =>
        {
            await Task.CompletedTask;
        }, TransactionOptions.ReadOnlyMode);

        otherConn.LastCreatedCommand.Should().BeNull();
    }

    [Fact]
    public async Task ReadOnlyOption_OnMySqlWhenCommandThrows_ShouldSwallowSilently()
    {
        var mysqlConn = new MySqlThrowingConnection();
        var mysqlFactory = new DelegateDbConnectionFactory(() => mysqlConn);
        var mysqlManager = new TransactionManager(mysqlFactory);

        Func<Task> act = () => mysqlManager.ExecuteAsync(async ctx =>
        {
            await Task.CompletedTask;
        }, TransactionOptions.ReadOnlyMode);

        await act.Should().NotThrowAsync();
    }
}
