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
