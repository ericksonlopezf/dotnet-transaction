// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Testing;
using Xunit;

namespace EricksonLopez.Transaction.Testing.Tests;

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
            manager.CurrentContext.Should().NotBeNull();
            executed1 = true;
            await Task.Yield();
        }, options, cts.Token);

        executed1.Should().BeTrue();
        manager.StartedTransactions.Should().HaveCount(1);
        manager.StartedTransactions[0].CommitCount.Should().Be(1);
        manager.CurrentContext.Should().BeNull();

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

    [Fact]
    public async Task BeginAsync_WhenTransactionDisposed_ShouldResetCurrentContextToNull()
    {
        var manager = new FakeTransactionManager();
        var tx = await manager.BeginAsync();

        manager.CurrentContext.Should().NotBeNull();
        manager.CurrentContext.Should().BeSameAs(tx.Context);

        await tx.DisposeAsync();

        manager.CurrentContext.Should().BeNull();
    }
}
