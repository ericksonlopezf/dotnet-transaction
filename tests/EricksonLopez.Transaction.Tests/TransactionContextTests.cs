// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Diagnostics;
using EricksonLopez.Transaction.Internal;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class TransactionContextTests
{
    private sealed class MockDbConnection : DbConnection
    {
        public DbCommand? LastCreatedCommand { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "TestDb";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotImplementedException();

        protected override DbCommand CreateDbCommand()
        {
            var cmd = new MockDbCommand();
            LastCreatedCommand = cmd;
            return cmd;
        }
    }

    private sealed class MockDbCommand : DbCommand
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
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class SaveThrowingTransaction : DbTransaction
    {
        private readonly DbConnection _conn;

        public SaveThrowingTransaction(DbConnection conn)
        {
            _conn = conn;
        }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _conn;

        public override void Commit() { }
        public override void Rollback() { }

        public override Task SaveAsync(string savepointName, CancellationToken cancellationToken = default)
            => Task.FromException(new NotSupportedException("Driver does not support direct save API"));
    }

    [Fact]
    public void Constructor_WhenDependenciesNull_ShouldThrowArgumentNullException()
    {
        var dbConn = Substitute.For<DbConnection>();
        var dbTx = Substitute.For<DbTransaction>();
        var sm = new TransactionStateMachine();

        Action act1 = () => _ = new TransactionContext(Guid.NewGuid(), null!, dbTx, TransactionIsolationLevel.ReadCommitted, sm, CancellationToken.None);
        Action act2 = () => _ = new TransactionContext(Guid.NewGuid(), dbConn, null!, TransactionIsolationLevel.ReadCommitted, sm, CancellationToken.None);
        Action act3 = () => _ = new TransactionContext(Guid.NewGuid(), dbConn, dbTx, TransactionIsolationLevel.ReadCommitted, null!, CancellationToken.None);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Properties_ShouldExposeConfiguredState()
    {
        var txId = Guid.NewGuid();
        var dbConn = Substitute.For<DbConnection>();
        var dbTx = Substitute.For<DbTransaction>();
        var sm = new TransactionStateMachine(TransactionState.Active);

        var context = new TransactionContext(txId, dbConn, dbTx, TransactionIsolationLevel.Serializable, sm, CancellationToken.None);

        context.TransactionId.Should().Be(txId);
        context.Connection.Should().BeSameAs(dbConn);
        context.Transaction.Should().BeSameAs(dbTx);
        context.IsolationLevel.Should().Be(TransactionIsolationLevel.Serializable);
        context.State.Should().Be(TransactionState.Active);
        context.Enlistments.Should().BeEmpty();

        await context.DisposeAsync();
        // Idempotent double dispose
        await context.DisposeAsync();
    }

    [Fact]
    public async Task Enlist_ShouldAddParticipant_AndThrowWhenNull()
    {
        var dbConn = Substitute.For<DbConnection>();
        var dbTx = Substitute.For<DbTransaction>();
        var sm = new TransactionStateMachine();
        var context = new TransactionContext(Guid.NewGuid(), dbConn, dbTx, TransactionIsolationLevel.ReadCommitted, sm, CancellationToken.None);

        Action nullAct = () => context.Enlist(null!);
        nullAct.Should().Throw<ArgumentNullException>();

        var hook = Substitute.For<ITransactionEnlistment>();
        context.Enlist(hook);

        context.Enlistments.Should().ContainSingle().Which.Should().BeSameAs(hook);

        await context.DisposeAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateSavepointAsync_WhenNameInvalid_ShouldThrowArgumentException(string? name)
    {
        var dbConn = Substitute.For<DbConnection>();
        var dbTx = Substitute.For<DbTransaction>();
        var sm = new TransactionStateMachine();
        var context = new TransactionContext(Guid.NewGuid(), dbConn, dbTx, TransactionIsolationLevel.ReadCommitted, sm, CancellationToken.None);

        Func<Task> act = () => context.CreateSavepointAsync(name!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must not be null or whitespace*");
        await context.DisposeAsync();
    }

    [Fact]
    public async Task CreateSavepointAsync_WhenDriverSupportsSave_ShouldRecordMetric()
    {
        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var dbConn = Substitute.For<DbConnection>();
        var dbTx = Substitute.For<DbTransaction>();
        var sm = new TransactionStateMachine();
        var context = new TransactionContext(Guid.NewGuid(), dbConn, dbTx, TransactionIsolationLevel.ReadCommitted, sm, CancellationToken.None);

        ISavepoint sp = await context.CreateSavepointAsync("sp_native", CancellationToken.None);

        sp.Should().NotBeNull();
        sp.Name.Should().Be("sp_native");
        recorded.Should().Contain("transactions.savepoints.created");

        await context.DisposeAsync();
    }

    [Fact]
    public async Task CreateSavepointAsync_WhenDriverThrowsNotSupported_ShouldFallbackToCommandAndRecordMetric()
    {
        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var conn = new MockDbConnection();
        var tx = new SaveThrowingTransaction(conn);
        var sm = new TransactionStateMachine();
        var context = new TransactionContext(Guid.NewGuid(), conn, tx, TransactionIsolationLevel.ReadCommitted, sm, CancellationToken.None);

        ISavepoint sp = await context.CreateSavepointAsync("sp_fallback", CancellationToken.None);

        sp.Should().NotBeNull();
        sp.Name.Should().Be("sp_fallback");
        conn.LastCreatedCommand.Should().NotBeNull();
        conn.LastCreatedCommand!.CommandText.Should().Be("SAVEPOINT sp_fallback;");
        recorded.Should().Contain("transactions.savepoints.created");

        await context.DisposeAsync();
    }

    [Fact]
    public async Task HookExecutions_ShouldInvokeAllEnlistments_AndSuppressSecondaryFailures()
    {
        var dbConn = Substitute.For<DbConnection>();
        var dbTx = Substitute.For<DbTransaction>();
        var sm = new TransactionStateMachine();
        var context = new TransactionContext(Guid.NewGuid(), dbConn, dbTx, TransactionIsolationLevel.ReadCommitted, sm, CancellationToken.None);

        var failingHook = Substitute.For<ITransactionEnlistment>();
        failingHook.When(h => h.AfterRollbackAsync(Arg.Any<ITransactionContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Rollback hook failure"));
        failingHook.When(h => h.OnExceptionAsync(Arg.Any<ITransactionContext>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("OnException hook failure"));

        var normalHook = Substitute.For<ITransactionEnlistment>();

        context.Enlist(failingHook);
        context.Enlist(normalHook);

        await context.ExecuteBeforeCommitHooksAsync(CancellationToken.None);
        await context.ExecuteAfterCommitHooksAsync(CancellationToken.None);

        Func<Task> rollbackAct = () => context.ExecuteAfterRollbackHooksAsync(CancellationToken.None);
        Func<Task> onExAct = () => context.ExecuteOnExceptionHooksAsync(new InvalidOperationException("test"), CancellationToken.None);

        await rollbackAct.Should().NotThrowAsync();
        await onExAct.Should().NotThrowAsync();

        await normalHook.Received(1).BeforeCommitAsync(context, Arg.Any<CancellationToken>());
        await normalHook.Received(1).AfterCommitAsync(context, Arg.Any<CancellationToken>());
        await normalHook.Received(1).AfterRollbackAsync(context, Arg.Any<CancellationToken>());
        await normalHook.Received(1).OnExceptionAsync(context, Arg.Any<Exception>(), Arg.Any<CancellationToken>());

        await context.DisposeAsync();
    }

    [Fact]
    public async Task Operations_AfterDisposal_ShouldThrowObjectDisposedException()
    {
        var dbConn = Substitute.For<DbConnection>();
        var dbTx = Substitute.For<DbTransaction>();
        var sm = new TransactionStateMachine();
        var context = new TransactionContext(Guid.NewGuid(), dbConn, dbTx, TransactionIsolationLevel.ReadCommitted, sm, CancellationToken.None);

        await context.DisposeAsync();

        var hook = Substitute.For<ITransactionEnlistment>();
        Action enlistAct = () => context.Enlist(hook);
        Func<Task> savepointAct = () => context.CreateSavepointAsync("sp1", CancellationToken.None);

        enlistAct.Should().Throw<ObjectDisposedException>();
        await savepointAct.Should().ThrowAsync<ObjectDisposedException>();
    }
}
