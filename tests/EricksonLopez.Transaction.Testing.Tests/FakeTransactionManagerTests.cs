// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Testing;
using Xunit;

namespace EricksonLopez.Transaction.Testing.Tests;

public sealed class FakeTransactionTests
{
    [Fact]
    public void FakeTransactionContext_PropertiesAndExceptions_ShouldWorkAsExpected()
    {
        var customId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var context = new FakeTransactionContext(customId, TransactionIsolationLevel.Serializable)
        {
            CancellationToken = cts.Token
        };

        context.TransactionId.Should().Be(customId);
        context.IsolationLevel.Should().Be(TransactionIsolationLevel.Serializable);
        context.CancellationToken.Should().Be(cts.Token);
        context.State.Should().Be(TransactionState.Active);

        context.State = TransactionState.Committed;
        context.State.Should().Be(TransactionState.Committed);

        Action accessConn = () => _ = context.Connection;
        Action accessTx = () => _ = context.Transaction;

        accessConn.Should().Throw<NotSupportedException>().WithMessage("FakeTransactionContext does not provide a physical DbConnection.");
        accessTx.Should().Throw<NotSupportedException>().WithMessage("FakeTransactionContext does not provide a physical DbTransaction.");
    }

    [Fact]
    public async Task FakeTransactionContext_EnlistAndSavepoints_ShouldWorkCorrectly()
    {
        var context = new FakeTransactionContext();
        Action nullEnlist = () => context.Enlist(null!);
        nullEnlist.Should().Throw<ArgumentNullException>().WithParameterName("enlistment");

        var hook = NSubstitute.Substitute.For<ITransactionEnlistment>();
        context.Enlist(hook);
        context.Enlistments.Should().ContainSingle().Which.Should().BeSameAs(hook);

        Func<Task> nullSp1 = () => context.CreateSavepointAsync(null!);
        Func<Task> nullSp2 = () => context.CreateSavepointAsync("   ");
        await nullSp1.Should().ThrowAsync<ArgumentException>();
        await nullSp2.Should().ThrowAsync<ArgumentException>();

        var sp = await context.CreateSavepointAsync("sp_1");
        sp.Name.Should().Be("sp_1");
        context.CreatedSavepoints.Should().ContainSingle().Which.Should().Be("sp_1");

        await sp.RollbackAsync();
        await sp.ReleaseAsync();
        await sp.DisposeAsync();

        await context.DisposeAsync();
    }

    [Fact]
    public void FakeTransaction_Constructor_WhenCustomContextPassed_ShouldUseCustomContext()
    {
        var customContext = new FakeTransactionContext(Guid.NewGuid(), TransactionIsolationLevel.Serializable);
        var tx = new FakeTransaction(customContext);

        tx.Context.Should().BeSameAs(customContext);
        tx.TransactionId.Should().Be(customContext.TransactionId);
        tx.State.Should().Be(TransactionState.Active);
    }

    [Fact]
    public async Task FakeTransaction_CommitAndRollback_WithExceptions_ShouldThrowAndSetFailedState()
    {
        var tx = new FakeTransaction();
        tx.TransactionId.Should().NotBeEmpty();
        tx.State.Should().Be(TransactionState.Active);
        tx.IsDisposed.Should().BeFalse();

        var sp = await tx.CreateSavepointAsync("sp_tx");
        sp.Name.Should().Be("sp_tx");

        var commitEx = new InvalidOperationException("Commit failure");
        tx.ExceptionToThrowOnCommit = commitEx;
        Func<Task> commitAct = () => tx.CommitAsync();
        var thrownCommit = await commitAct.Should().ThrowAsync<InvalidOperationException>();
        thrownCommit.Which.Should().BeSameAs(commitEx);
        tx.State.Should().Be(TransactionState.Failed);
        tx.CommitCount.Should().Be(1);

        var rollbackEx = new InvalidOperationException("Rollback failure");
        tx.ExceptionToThrowOnRollback = rollbackEx;
        Func<Task> rollbackAct = () => tx.RollbackAsync();
        var thrownRollback = await rollbackAct.Should().ThrowAsync<InvalidOperationException>();
        thrownRollback.Which.Should().BeSameAs(rollbackEx);
        tx.State.Should().Be(TransactionState.Failed);
        tx.RollbackCount.Should().Be(1);

        // Dispose on Failed state should transition to Disposed
        await tx.DisposeAsync();
        tx.State.Should().Be(TransactionState.Disposed);
        tx.IsDisposed.Should().BeTrue();

        // Double dispose is no-op
        await tx.DisposeAsync();
    }

    [Fact]
    public async Task FakeTransaction_DisposeWhenActive_ShouldTransitionToRolledBack()
    {
        var tx = new FakeTransaction();
        tx.State.Should().Be(TransactionState.Active);

        await tx.DisposeAsync();

        tx.State.Should().Be(TransactionState.RolledBack);
        tx.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task FakeTransaction_NormalCommitAndRollback_ShouldTransitionStates()
    {
        var tx1 = new FakeTransaction();
        await tx1.CommitAsync();
        tx1.State.Should().Be(TransactionState.Committed);
        tx1.CommitCount.Should().Be(1);
        await tx1.DisposeAsync();
        tx1.State.Should().Be(TransactionState.Committed);

        var tx2 = new FakeTransaction();
        await tx2.RollbackAsync();
        tx2.State.Should().Be(TransactionState.RolledBack);
        tx2.RollbackCount.Should().Be(1);
        await tx2.DisposeAsync();
        tx2.State.Should().Be(TransactionState.RolledBack);
    }
}

public sealed class FakeTransactionManagerTests
{
    [Fact]
    public async Task ExecuteAsync_NullArguments_ShouldThrowArgumentNullException()
    {
        var manager = new FakeTransactionManager();
        Func<ITransactionContext, Task> nullOp1 = null!;
        Func<Task> nullOp2 = null!;
        Func<ITransactionContext, Task<int>> nullOp3 = null!;
        Func<Task<int>> nullOp4 = null!;

        Func<Task> act1 = () => manager.ExecuteAsync(nullOp1);
        Func<Task> act2 = () => manager.ExecuteAsync(nullOp2);
        Func<Task> act3 = () => manager.ExecuteAsync(nullOp3);
        Func<Task> act4 = () => manager.ExecuteAsync(nullOp4);

        await act1.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
        await act2.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
        await act3.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
        await act4.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
    }

    [Fact]
    public async Task ExecuteAsync_AllOverloads_ShouldExecuteOperationAndCommit()
    {
        var manager = new FakeTransactionManager();
        using var cts = new CancellationTokenSource();
        var options = new TransactionOptions { IsolationLevel = TransactionIsolationLevel.Serializable };

        // 1. Func<ITransactionContext, Task>
        bool executed1 = false;
        await manager.ExecuteAsync(async context =>
        {
            context.Should().NotBeNull();
            context.IsolationLevel.Should().Be(TransactionIsolationLevel.Serializable);
            context.CancellationToken.Should().Be(cts.Token);
            executed1 = true;
            await Task.Yield();
        }, options, cts.Token);

        executed1.Should().BeTrue();
        manager.StartedTransactions.Should().HaveCount(1);
        manager.StartedTransactions[0].CommitCount.Should().Be(1);
        manager.CurrentContext.Should().NotBeNull();

        // 2. Func<Task>
        bool executed2 = false;
        await manager.ExecuteAsync(async () =>
        {
            executed2 = true;
            await Task.Yield();
        }, options, cts.Token);

        executed2.Should().BeTrue();
        manager.StartedTransactions.Should().HaveCount(2);
        manager.StartedTransactions[1].CommitCount.Should().Be(1);

        // 3. Func<ITransactionContext, Task<TResult>>
        bool executed3 = false;
        string res1 = await manager.ExecuteAsync(async context =>
        {
            context.Should().NotBeNull();
            executed3 = true;
            await Task.Yield();
            return "res1";
        }, options, cts.Token);

        executed3.Should().BeTrue();
        res1.Should().Be("res1");
        manager.StartedTransactions.Should().HaveCount(3);
        manager.StartedTransactions[2].CommitCount.Should().Be(1);

        // 4. Func<Task<TResult>>
        bool executed4 = false;
        int res2 = await manager.ExecuteAsync(async () =>
        {
            executed4 = true;
            await Task.Yield();
            return 42;
        }, options, cts.Token);

        executed4.Should().BeTrue();
        res2.Should().Be(42);
        manager.StartedTransactions.Should().HaveCount(4);
        manager.StartedTransactions[3].CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task BeginAsync_OptionsAndCancellation_ShouldBePropagated()
    {
        var manager = new FakeTransactionManager();
        using var cts = new CancellationTokenSource();

        // Default options
        var txDefault = await manager.BeginAsync(null, cts.Token);
        txDefault.Context.IsolationLevel.Should().Be(TransactionIsolationLevel.ReadCommitted);
        txDefault.Context.CancellationToken.Should().Be(cts.Token);

        // Custom options
        var txCustom = await manager.BeginAsync(new TransactionOptions { IsolationLevel = TransactionIsolationLevel.Snapshot });
        txCustom.Context.IsolationLevel.Should().Be(TransactionIsolationLevel.Snapshot);
    }

    [Fact]
    public async Task BeginAsync_WithExceptionToThrowOnCommit_ShouldPropagateToTransaction()
    {
        var manager = new FakeTransactionManager();
        var expectedEx = new InvalidOperationException("Global commit error");
        manager.ExceptionToThrowOnCommit = expectedEx;

        var tx = (FakeTransaction)await manager.BeginAsync();
        tx.ExceptionToThrowOnCommit.Should().BeSameAs(expectedEx);

        Func<Task> act = () => tx.CommitAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
