// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Diagnostics;
using EricksonLopez.Transaction.Exceptions;
using EricksonLopez.Transaction.Internal;
using NSubstitute;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EricksonLopez.Transaction.Tests;

public sealed class PhysicalTransactionTests : IDisposable
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly TransactionStateMachine _stateMachine;
    private readonly TransactionContext _context;

    public PhysicalTransactionTests()
    {
        _connection = Substitute.For<DbConnection>();
        _transaction = Substitute.For<DbTransaction>();
        _stateMachine = new TransactionStateMachine(TransactionState.Active);
        _context = new TransactionContext(
            Guid.NewGuid(),
            _connection,
            _transaction,
            TransactionIsolationLevel.ReadCommitted,
            _stateMachine,
            CancellationToken.None);
    }

    public void Dispose()
    {
        _context.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class ThrowingDisposeConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "TestDb";
        public override string DataSource => "localhost";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
        protected override DbCommand CreateDbCommand() => throw new NotImplementedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new InvalidOperationException("Conn dispose error");
            }
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingDisposeTransaction : DbTransaction
    {
        private readonly DbConnection _conn;
        public ThrowingDisposeTransaction(DbConnection conn) => _conn = conn;
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _conn;
        public override void Commit() { }
        public override void Rollback() { }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                throw new InvalidOperationException("Tx dispose error");
            }
            base.Dispose(disposing);
        }
    }

    [Fact]
    public void Constructor_WhenDependenciesNull_ShouldThrowArgumentNullException()
    {
        Action act1 = () => _ = new PhysicalTransaction(null!, _stateMachine, _connection, _transaction, true);
        Action act2 = () => _ = new PhysicalTransaction(_context, null!, _connection, _transaction, true);
        Action act3 = () => _ = new PhysicalTransaction(_context, _stateMachine, null!, _transaction, true);
        Action act4 = () => _ = new PhysicalTransaction(_context, _stateMachine, _connection, null!, true);

        act1.Should().Throw<ArgumentNullException>().WithParameterName("context");
        act2.Should().Throw<ArgumentNullException>().WithParameterName("stateMachine");
        act3.Should().Throw<ArgumentNullException>().WithParameterName("connection");
        act4.Should().Throw<ArgumentNullException>().WithParameterName("transaction");
    }

    [Fact]
    public void Properties_ShouldExposeContextAndStateMachineState()
    {
        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true, transactionName: "CustomTx");

        tx.TransactionId.Should().Be(_context.TransactionId);
        tx.Context.Should().BeSameAs(_context);
        tx.State.Should().Be(TransactionState.Active);

        recorded.Should().Contain("transactions.started");
    }

    [Fact]
    public async Task CommitAsync_ShouldInvokeHooksAndTransitionToCommitted()
    {
        var hook = Substitute.For<ITransactionEnlistment>();
        _context.Enlist(hook);

        var events = new List<string>();
        hook.When(h => h.BeforeCommitAsync(Arg.Any<ITransactionContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => events.Add("BeforeCommit"));
        _transaction.When(t => t.CommitAsync(Arg.Any<CancellationToken>()))
            .Do(_ => events.Add("DbCommit"));
        hook.When(h => h.AfterCommitAsync(Arg.Any<ITransactionContext>(), Arg.Any<CancellationToken>()))
            .Do(_ => events.Add("AfterCommit"));

        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        await tx.CommitAsync(CancellationToken.None);

        tx.State.Should().Be(TransactionState.Committed);
        events.Should().ContainInOrder("BeforeCommit", "DbCommit", "AfterCommit");
        recorded.Should().Contain("transactions.committed");
    }

    [Fact]
    public async Task CommitAsync_WithActivityListener_ShouldRecordActivityStatus()
    {
        Activity? captured = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TransactionDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => captured = a
        };
        ActivitySource.AddActivityListener(listener);

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);
        await tx.CommitAsync(CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.OperationName.Should().Be("Transaction.Execute");
        captured.Status.Should().Be(ActivityStatusCode.Ok);
        captured.TagObjects.First(t => t.Key == "transaction.outcome").Value.Should().Be("committed");
    }

    [Fact]
    public async Task CommitAsync_WhenDbTransactionThrows_ShouldTransitionToFailedAndThrowAmbiguousException()
    {
        var hook = Substitute.For<ITransactionEnlistment>();
        _context.Enlist(hook);

        var dbException = new TimeoutException("Database lost connectivity during commit acknowledgment.");
        _transaction.When(t => t.CommitAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw dbException);

        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        Activity? captured = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TransactionDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => captured = a
        };
        ActivitySource.AddActivityListener(listener);

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        Func<Task> act = () => tx.CommitAsync(CancellationToken.None);

        var ex = await act.Should().ThrowAsync<TransactionCommitException>();
        ex.Which.IsAmbiguous.Should().BeTrue();
        ex.Which.InnerException.Should().BeSameAs(dbException);
        ex.Which.Message.Should().Contain(_context.TransactionId.ToString());
        tx.State.Should().Be(TransactionState.Failed);
        await hook.Received(1).OnExceptionAsync(_context, dbException, Arg.Any<CancellationToken>());

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(ActivityStatusCode.Error);
        captured.TagObjects.First(t => t.Key == "transaction.outcome").Value.Should().Be("failed");
        recorded.Should().Contain("transactions.failed");
    }

    [Fact]
    public async Task CommitAsync_WhenDbTransactionThrowsTransactionStateException_ShouldRethrowDirectly()
    {
        var stateException = new TransactionStateException(TransactionState.Active, "Commit");
        _transaction.When(t => t.CommitAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw stateException);

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        Func<Task> act = () => tx.CommitAsync(CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<TransactionStateException>();
        thrown.Which.Should().BeSameAs(stateException);
    }

    [Fact]
    public async Task CommitAsync_WhenCancelled_ShouldTransitionToFailedAndRethrow()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _transaction.When(t => t.CommitAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new OperationCanceledException(cts.Token));

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        Func<Task> act = () => tx.CommitAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        tx.State.Should().Be(TransactionState.Failed);
    }

    [Fact]
    public async Task RollbackAsync_ShouldInvokeHooksAndTransitionToRolledBack()
    {
        var hook = Substitute.For<ITransactionEnlistment>();
        _context.Enlist(hook);

        var recorded = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add(inst.Name));
        meterListener.Start();

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        await tx.RollbackAsync(CancellationToken.None);

        tx.State.Should().Be(TransactionState.RolledBack);
        await hook.Received(1).AfterRollbackAsync(_context, Arg.Any<CancellationToken>());
        recorded.Should().Contain("transactions.rolled_back");
    }

    [Fact]
    public async Task RollbackAsync_WithActivityListener_ShouldRecordActivityOutcome()
    {
        Activity? captured = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TransactionDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => captured = a
        };
        ActivitySource.AddActivityListener(listener);

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);
        await tx.RollbackAsync(CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TagObjects.First(t => t.Key == "transaction.outcome").Value.Should().Be("rolled_back");
    }

    [Fact]
    public async Task RollbackAsync_WhenDbTransactionThrows_ShouldTransitionToFailedAndThrowRollbackException()
    {
        var hook = Substitute.For<ITransactionEnlistment>();
        _context.Enlist(hook);

        var dbException = new InvalidOperationException("Connection broken.");
        _transaction.When(t => t.RollbackAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw dbException);

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        Func<Task> act = () => tx.RollbackAsync(CancellationToken.None);

        var ex = await act.Should().ThrowAsync<TransactionRollbackException>();
        ex.Which.InnerException.Should().BeSameAs(dbException);
        ex.Which.Message.Should().Contain(_context.TransactionId.ToString());
        tx.State.Should().Be(TransactionState.Failed);
        await hook.Received(1).OnExceptionAsync(_context, dbException, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RollbackAsync_WhenDbTransactionThrowsTransactionStateException_ShouldRethrowDirectly()
    {
        var stateException = new TransactionStateException(TransactionState.Active, "Rollback");
        _transaction.When(t => t.RollbackAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw stateException);

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        Func<Task> act = () => tx.RollbackAsync(CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<TransactionStateException>();
        thrown.Which.Should().BeSameAs(stateException);
    }

    [Fact]
    public async Task RollbackAsync_WhenDbTransactionThrowsOperationCanceledException_ShouldTransitionToFailedAndRethrow()
    {
        _transaction.When(t => t.RollbackAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new OperationCanceledException());

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        Func<Task> act = () => tx.RollbackAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        tx.State.Should().Be(TransactionState.Failed);
    }

    [Fact]
    public async Task CreateSavepointAsync_ShouldDelegateToContext()
    {
        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        ISavepoint sp = await tx.CreateSavepointAsync("sp1", CancellationToken.None);
        sp.Should().NotBeNull();
        sp.Name.Should().Be("sp1");
    }

    [Fact]
    public async Task DisposeAsync_WhenActive_ShouldRollbackAndDisposeResources()
    {
        TransactionState capturedStateInHook = TransactionState.Created;
        var hook = Substitute.For<ITransactionEnlistment>();
        hook.When(h => h.AfterRollbackAsync(Arg.Any<ITransactionContext>(), Arg.Any<CancellationToken>()))
            .Do(call => capturedStateInHook = ((ITransactionContext)call[0]).State);
        _context.Enlist(hook);

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        await tx.DisposeAsync();

        capturedStateInHook.Should().Be(TransactionState.RolledBack);
        tx.State.Should().Be(TransactionState.Disposed);
        await _transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await hook.Received(1).AfterRollbackAsync(_context, Arg.Any<CancellationToken>());
        await _connection.Received(1).DisposeAsync();
        await _transaction.Received(1).DisposeAsync();

        // Second dispose should be a complete no-op
        await tx.DisposeAsync();
        await _connection.Received(1).DisposeAsync();
        await _transaction.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenNotOwningConnection_ShouldNotDisposeConnection()
    {
        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: false);

        await tx.DisposeAsync();

        tx.State.Should().Be(TransactionState.Disposed);
        await _connection.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenRollbackThrows_ShouldSwallowSilently()
    {
        _transaction.When(t => t.RollbackAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Network failure during rollback on dispose"));

        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        Func<Task> act = async () => await tx.DisposeAsync();

        await act.Should().NotThrowAsync();
        tx.State.Should().Be(TransactionState.Disposed);
    }

    [Fact]
    public async Task DisposeAsync_WhenTransactionOrConnectionDisposeThrows_ShouldSwallowSilently()
    {
        var throwingConn = new ThrowingDisposeConnection();
        var throwingTx = new ThrowingDisposeTransaction(throwingConn);

        var machine = new TransactionStateMachine(TransactionState.Committed);
        var context = new TransactionContext(Guid.NewGuid(), throwingConn, throwingTx, TransactionIsolationLevel.ReadCommitted, machine, CancellationToken.None);
        var tx = new PhysicalTransaction(context, machine, throwingConn, throwingTx, ownsConnection: true);

        Func<Task> act = async () => await tx.DisposeAsync();

        await act.Should().NotThrowAsync();
        tx.State.Should().Be(TransactionState.Disposed);
    }

    [Fact]
    public async Task PostDisposal_Operations_ShouldThrowObjectDisposedException()
    {
        var tx = new PhysicalTransaction(_context, _stateMachine, _connection, _transaction, ownsConnection: true);

        await tx.DisposeAsync();

        Func<Task> commitAct = () => tx.CommitAsync(CancellationToken.None);
        Func<Task> rollbackAct = () => tx.RollbackAsync(CancellationToken.None);
        Func<Task> savepointAct = () => tx.CreateSavepointAsync("sp1", CancellationToken.None);

        await commitAct.Should().ThrowAsync<ObjectDisposedException>();
        await rollbackAct.Should().ThrowAsync<ObjectDisposedException>();
        await savepointAct.Should().ThrowAsync<ObjectDisposedException>();
    }
}
