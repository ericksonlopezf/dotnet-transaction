// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Exceptions;
using EricksonLopez.Transaction.Internal;
using Microsoft.Data.Sqlite;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class SavepointTransactionScopeTests
{
    [Fact]
    public void Constructor_WhenArgumentsNull_ShouldThrowArgumentNullException()
    {
        var context = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();

        Action act1 = () => _ = new SavepointTransactionScope(null!, savepoint);
        Action act2 = () => _ = new SavepointTransactionScope(context, null!);

        act1.Should().Throw<ArgumentNullException>().WithParameterName("parentContext");
        act2.Should().Throw<ArgumentNullException>().WithParameterName("savepoint");
    }

    [Fact]
    public async Task SavepointScope_Commit_ShouldReleaseSavepoint()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var initCmd = connection.CreateCommand();
        initCmd.CommandText = "CREATE TABLE items (id INT, val TEXT);";
        await initCmd.ExecuteNonQueryAsync();

        await using var dbTx = await connection.BeginTransactionAsync();
        var machine = new TransactionStateMachine(TransactionState.Active);
        var context = new TransactionContext(Guid.NewGuid(), connection, dbTx, TransactionIsolationLevel.ReadCommitted, machine, CancellationToken.None);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)dbTx;
            cmd.CommandText = "INSERT INTO items VALUES (1, 'Outer');";
            await cmd.ExecuteNonQueryAsync();
        }

        ISavepoint savepoint = await context.CreateSavepointAsync("nested_sp");
        var scope = new SavepointTransactionScope(context, savepoint);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)dbTx;
            cmd.CommandText = "INSERT INTO items VALUES (2, 'Inner');";
            await cmd.ExecuteNonQueryAsync();
        }

        await scope.CommitAsync();

        scope.State.Should().Be(TransactionState.Committed);
        ((Savepoint)savepoint).IsReleased.Should().BeTrue();
        ((Savepoint)savepoint).IsRolledBack.Should().BeFalse();

        await dbTx.CommitAsync();

        await using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM items;";
            long count = (long)(await countCmd.ExecuteScalarAsync())!;
            count.Should().Be(2);
        }

        await context.DisposeAsync();
    }

    [Fact]
    public async Task SavepointScope_Commit_WhenReleaseThrows_ShouldThrowTransactionCommitException()
    {
        var context = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();
        savepoint.Name.Returns("sp_err");
        var error = new InvalidOperationException("Release error");
        savepoint.When(s => s.ReleaseAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw error);

        var scope = new SavepointTransactionScope(context, savepoint);

        Func<Task> act = () => scope.CommitAsync();

        var ex = await act.Should().ThrowAsync<TransactionCommitException>();
        ex.Which.Message.Should().Contain("sp_err");
        ex.Which.InnerException.Should().BeSameAs(error);
        scope.State.Should().Be(TransactionState.Failed);
    }

    [Fact]
    public async Task SavepointScope_Rollback_ShouldRollbackSavepoint()
    {
        var context = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();
        savepoint.Name.Returns("sp1");

        var scope = new SavepointTransactionScope(context, savepoint);

        scope.TransactionId.Should().NotBeEmpty();
        scope.Context.Should().BeSameAs(context);
        scope.State.Should().Be(TransactionState.Active);

        await scope.RollbackAsync();

        scope.State.Should().Be(TransactionState.RolledBack);
        await savepoint.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavepointScope_Rollback_WhenThrows_ShouldThrowTransactionRollbackException()
    {
        var context = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();
        savepoint.Name.Returns("sp_rb_err");
        var error = new InvalidOperationException("Rollback error");
        savepoint.When(s => s.RollbackAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw error);

        var scope = new SavepointTransactionScope(context, savepoint);

        Func<Task> act = () => scope.RollbackAsync();

        var ex = await act.Should().ThrowAsync<TransactionRollbackException>();
        ex.Which.Message.Should().Contain("sp_rb_err");
        ex.Which.InnerException.Should().BeSameAs(error);
        scope.State.Should().Be(TransactionState.Failed);
    }

    [Fact]
    public async Task SavepointScope_CreateSavepoint_ShouldDelegateToContext()
    {
        var context = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();
        var nestedSavepoint = Substitute.For<ISavepoint>();
        context.CreateSavepointAsync("nested_sp", Arg.Any<CancellationToken>()).Returns(nestedSavepoint);

        var scope = new SavepointTransactionScope(context, savepoint);

        ISavepoint result = await scope.CreateSavepointAsync("nested_sp");

        result.Should().BeSameAs(nestedSavepoint);
    }

    [Fact]
    public async Task SavepointScope_DisposeAsync_WhenActive_ShouldRollbackAndDisposeSavepoint()
    {
        var context = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();

        var scope = new SavepointTransactionScope(context, savepoint);

        await scope.DisposeAsync();

        scope.State.Should().Be(TransactionState.Disposed);
        await savepoint.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await savepoint.Received(1).DisposeAsync();

        // Idempotent second dispose
        await scope.DisposeAsync();
        await savepoint.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task SavepointScope_DisposeAsync_WhenRollbackThrows_ShouldSwallowSilently()
    {
        var context = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();
        savepoint.When(s => s.RollbackAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Rollback failed during disposal"));

        var scope = new SavepointTransactionScope(context, savepoint);

        Func<Task> act = async () => await scope.DisposeAsync();

        await act.Should().NotThrowAsync();
        scope.State.Should().Be(TransactionState.Disposed);
    }

    [Fact]
    public async Task SavepointScope_OperationsAfterDisposal_ShouldThrowObjectDisposedException()
    {
        var context = Substitute.For<ITransactionContext>();
        var savepoint = Substitute.For<ISavepoint>();

        var scope = new SavepointTransactionScope(context, savepoint);
        await scope.DisposeAsync();

        Func<Task> commitAct = () => scope.CommitAsync();
        Func<Task> rollbackAct = () => scope.RollbackAsync();
        Func<Task> spAct = () => scope.CreateSavepointAsync("sp1");

        await commitAct.Should().ThrowAsync<ObjectDisposedException>();
        await rollbackAct.Should().ThrowAsync<ObjectDisposedException>();
        await spAct.Should().ThrowAsync<ObjectDisposedException>();
    }
}
