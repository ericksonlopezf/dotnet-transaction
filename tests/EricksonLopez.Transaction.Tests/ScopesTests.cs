// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Exceptions;
using EricksonLopez.Transaction.Internal;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class ScopesTests
{
    [Fact]
    public void AmbientTransactionScope_WhenArgumentsNull_ShouldThrowArgumentNullException()
    {
        var innerTx = Substitute.For<ITransaction>();
        var holder = new AsyncLocal<ITransactionContext?>();

        Action act1 = () => _ = new AmbientTransactionScope(null!, null, holder);
        Action act2 = () => _ = new AmbientTransactionScope(innerTx, null, null!);

        act1.Should().Throw<ArgumentNullException>().WithParameterName("innerTransaction");
        act2.Should().Throw<ArgumentNullException>().WithParameterName("ambientContextHolder");
    }

    [Fact]
    public async Task AmbientTransactionScope_ShouldManageAmbientContextAndDelegate()
    {
        var holder = new AsyncLocal<ITransactionContext?>();
        var previousContext = Substitute.For<ITransactionContext>();
        holder.Value = previousContext;

        var innerContext = Substitute.For<ITransactionContext>();
        var innerTx = Substitute.For<ITransaction>();
        innerTx.Context.Returns(innerContext);
        innerTx.TransactionId.Returns(Guid.NewGuid());
        innerTx.State.Returns(TransactionState.Active);

        var scope = new AmbientTransactionScope(innerTx, previousContext, holder);

        holder.Value.Should().BeSameAs(innerContext);
        scope.TransactionId.Should().Be(innerTx.TransactionId);
        scope.Context.Should().BeSameAs(innerContext);
        scope.State.Should().Be(TransactionState.Active);

        await scope.CommitAsync();
        await scope.RollbackAsync();
        await scope.CreateSavepointAsync("sp1");

        await innerTx.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await innerTx.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await innerTx.Received(1).CreateSavepointAsync("sp1", Arg.Any<CancellationToken>());

        await scope.DisposeAsync();
        await innerTx.Received(1).DisposeAsync();

        // Second dispose should be a complete no-op (idempotent, innerTx.DisposeAsync called only once)
        await scope.DisposeAsync();
        await innerTx.Received(1).DisposeAsync();
    }

    [Fact]
    public void JoinExistingTransactionScope_WhenParentNull_ShouldThrowArgumentNullException()
    {
        Action act = () => _ = new JoinExistingTransactionScope(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("parentContext");
    }

    [Fact]
    public async Task JoinExistingTransactionScope_ShouldParticipateWithoutSavepoint()
    {
        var parentContext = Substitute.For<ITransactionContext>();
        var txId = Guid.NewGuid();
        parentContext.TransactionId.Returns(txId);

        var scope = new JoinExistingTransactionScope(parentContext);

        scope.TransactionId.Should().Be(txId);
        scope.Context.Should().BeSameAs(parentContext);
        scope.State.Should().Be(TransactionState.Active);

        await scope.CreateSavepointAsync("sp_nested");
        await parentContext.Received(1).CreateSavepointAsync("sp_nested", Arg.Any<CancellationToken>());

        await scope.CommitAsync();
        scope.State.Should().Be(TransactionState.Committed);

        await scope.DisposeAsync();
        scope.State.Should().Be(TransactionState.Disposed);

        // Second dispose is no-op
        await scope.DisposeAsync();
        scope.State.Should().Be(TransactionState.Disposed);
    }

    [Fact]
    public async Task JoinExistingTransactionScope_WhenRolledBack_ShouldTransitionToRolledBack()
    {
        var parentContext = Substitute.For<ITransactionContext>();
        var scope = new JoinExistingTransactionScope(parentContext);

        await scope.RollbackAsync();
        scope.State.Should().Be(TransactionState.RolledBack);

        await scope.DisposeAsync();
        scope.State.Should().Be(TransactionState.Disposed);

        // Second dispose is no-op
        await scope.DisposeAsync();
    }

    [Fact]
    public async Task JoinExistingTransactionScope_WhenDisposedWithoutCommit_ShouldTransitionToDisposed()
    {
        var parentContext = Substitute.For<ITransactionContext>();
        var scope = new JoinExistingTransactionScope(parentContext);

        scope.State.Should().Be(TransactionState.Active);
        await scope.DisposeAsync();
        scope.State.Should().Be(TransactionState.Disposed);

        // Idempotent second dispose
        await scope.DisposeAsync();
    }

    [Fact]
    public async Task JoinExistingTransactionScope_OperationsAfterDisposal_ShouldThrowObjectDisposedException()
    {
        var parentContext = Substitute.For<ITransactionContext>();
        var scope = new JoinExistingTransactionScope(parentContext);
        await scope.DisposeAsync();

        Func<Task> commitAct = () => scope.CommitAsync();
        Func<Task> rollbackAct = () => scope.RollbackAsync();
        Func<Task> spAct = () => scope.CreateSavepointAsync("sp1");

        await commitAct.Should().ThrowAsync<ObjectDisposedException>();
        await rollbackAct.Should().ThrowAsync<ObjectDisposedException>();
        await spAct.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SavepointTransactionScope_WhenCommitFails_ShouldThrowTransactionCommitException()
    {
        var parentContext = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();
        savepoint.Name.Returns("sp_fail");
        savepoint.When(s => s.ReleaseAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Release failed"));

        var scope = new SavepointTransactionScope(parentContext, savepoint);

        Func<Task> act = () => scope.CommitAsync();

        await act.Should().ThrowAsync<TransactionCommitException>();
        scope.State.Should().Be(TransactionState.Failed);
    }

    [Fact]
    public async Task SavepointTransactionScope_WhenRollbackFails_ShouldThrowTransactionRollbackException()
    {
        var parentContext = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();
        savepoint.Name.Returns("sp_fail");
        savepoint.When(s => s.RollbackAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Rollback failed"));

        var scope = new SavepointTransactionScope(parentContext, savepoint);

        Func<Task> act = () => scope.RollbackAsync();

        await act.Should().ThrowAsync<TransactionRollbackException>();
        scope.State.Should().Be(TransactionState.Failed);
    }

    [Fact]
    public void SuppressedTransactionScope_WhenHolderNull_ShouldThrowArgumentNullException()
    {
        Action act = () => _ = new SuppressedTransactionScope(null, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("ambientContextHolder");
    }

    [Fact]
    public async Task SuppressedTransactionScope_CommitAndDispose_ShouldWorkCorrectly()
    {
        var holder = new AsyncLocal<ITransactionContext?>();
        var previousContext = Substitute.For<ITransactionContext>();
        holder.Value = previousContext;

        var scope = new SuppressedTransactionScope(previousContext, holder);

        holder.Value.Should().BeNull();
        scope.TransactionId.Should().NotBeEmpty();
        scope.State.Should().Be(TransactionState.Active);

        Action accessContext = () => { _ = scope.Context; };
        accessContext.Should().Throw<InvalidOperationException>()
            .WithMessage("A suppressed transaction scope does not provide an active ITransactionContext.");

        Func<Task> savepointAct = () => scope.CreateSavepointAsync("sp1");
        await savepointAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot create savepoints on a suppressed transaction scope.");

        await scope.CommitAsync();
        scope.State.Should().Be(TransactionState.Committed);

        await scope.DisposeAsync();
        scope.State.Should().Be(TransactionState.Committed);

        // Second dispose is no-op
        await scope.DisposeAsync();
    }

    [Fact]
    public async Task SuppressedTransactionScope_RollbackAndDispose_ShouldWorkCorrectly()
    {
        var holder = new AsyncLocal<ITransactionContext?>();
        var previousContext = Substitute.For<ITransactionContext>();
        holder.Value = previousContext;

        var scope = new SuppressedTransactionScope(previousContext, holder);

        await scope.RollbackAsync();
        scope.State.Should().Be(TransactionState.RolledBack);

        await scope.DisposeAsync();
        scope.State.Should().Be(TransactionState.RolledBack);

        // Second dispose is no-op
        await scope.DisposeAsync();
    }

    [Fact]
    public async Task SuppressedTransactionScope_DisposeWithoutCommit_ShouldTransitionToDisposed()
    {
        var holder = new AsyncLocal<ITransactionContext?>();
        var scope = new SuppressedTransactionScope(null, holder);

        scope.State.Should().Be(TransactionState.Active);
        await scope.DisposeAsync();
        scope.State.Should().Be(TransactionState.Disposed);

        // Second dispose is no-op
        await scope.DisposeAsync();
    }

    [Fact]
    public async Task SuppressedTransactionScope_OperationsAfterDisposal_ShouldThrowObjectDisposedException()
    {
        var holder = new AsyncLocal<ITransactionContext?>();
        var scope = new SuppressedTransactionScope(null, holder);
        await scope.DisposeAsync();

        Func<Task> commitAct = () => scope.CommitAsync();
        Func<Task> rollbackAct = () => scope.RollbackAsync();
        Func<Task> savepointAct = () => scope.CreateSavepointAsync("sp1");

        await commitAct.Should().ThrowAsync<ObjectDisposedException>();
        await rollbackAct.Should().ThrowAsync<ObjectDisposedException>();
        await savepointAct.Should().ThrowAsync<ObjectDisposedException>();
    }
}
