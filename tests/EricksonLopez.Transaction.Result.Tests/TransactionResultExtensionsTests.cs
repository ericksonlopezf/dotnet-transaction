// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Result;
using EricksonLopez.Transaction.Result;
using EricksonLopez.Transaction.Testing;
using Xunit;
using ResultInstance = EricksonLopez.Result.Result;

namespace EricksonLopez.Transaction.Result.Tests;

public sealed class TransactionResultExtensionsTests
{
    [Fact]
    public async Task ExecuteResultAsync_WhenArgumentsNull_ShouldThrowArgumentNullException()
    {
        ITransactionManager nullManager = null!;
        var fakeManager = new FakeTransactionManager();
        Func<ITransactionContext, Task<ResultInstance>> nullContextOp = null!;
        Func<Task<ResultInstance>> nullOp = null!;
        Func<ITransactionContext, Task<Result<int>>> nullContextGenOp = null!;
        Func<Task<Result<int>>> nullGenOp = null!;

        Func<Task> act1 = () => nullManager.ExecuteResultAsync(async c => ResultInstance.Success());
        Func<Task> act2 = () => fakeManager.ExecuteResultAsync(nullContextOp);
        Func<Task> act3 = () => nullManager.ExecuteResultAsync(async () => ResultInstance.Success());
        Func<Task> act4 = () => fakeManager.ExecuteResultAsync(nullOp);

        Func<Task> act5 = () => nullManager.ExecuteResultAsync<int>(async c => Result<int>.Success(1));
        Func<Task> act6 = () => fakeManager.ExecuteResultAsync(nullContextGenOp);
        Func<Task> act7 = () => nullManager.ExecuteResultAsync<int>(async () => Result<int>.Success(1));
        Func<Task> act8 = () => fakeManager.ExecuteResultAsync(nullGenOp);

        await act1.Should().ThrowAsync<ArgumentNullException>().WithParameterName("manager");
        await act2.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
        await act3.Should().ThrowAsync<ArgumentNullException>().WithParameterName("manager");
        await act4.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");

        await act5.Should().ThrowAsync<ArgumentNullException>().WithParameterName("manager");
        await act6.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
        await act7.Should().ThrowAsync<ArgumentNullException>().WithParameterName("manager");
        await act8.Should().ThrowAsync<ArgumentNullException>().WithParameterName("operation");
    }

    [Fact]
    public async Task ExecuteResultAsync_NonGeneric_WithContext_WhenSuccess_ShouldCommitTransaction()
    {
        var fakeManager = new FakeTransactionManager();
        using var cts = new CancellationTokenSource();
        var options = TransactionOptions.Default;

        ResultInstance result = await fakeManager.ExecuteResultAsync(async context =>
        {
            context.Should().NotBeNull();
            await Task.Yield();
            return ResultInstance.Success();
        }, options, cts.Token);

        result.IsSuccess.Should().BeTrue();
        fakeManager.StartedTransactions.Should().HaveCount(1);
        fakeManager.StartedTransactions[0].CommitCount.Should().Be(1);
        fakeManager.StartedTransactions[0].RollbackCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteResultAsync_NonGeneric_WithContext_WhenFailure_ShouldRollbackTransaction()
    {
        var fakeManager = new FakeTransactionManager();
        var expectedError = Error.Validation("INVALID_INPUT", "The input was invalid.");

        ResultInstance result = await fakeManager.ExecuteResultAsync(async context =>
        {
            await Task.Yield();
            return ResultInstance.Failure(expectedError);
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
        fakeManager.StartedTransactions.Should().HaveCount(1);
        fakeManager.StartedTransactions[0].CommitCount.Should().Be(0);
        fakeManager.StartedTransactions[0].RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteResultAsync_Generic_WithContext_WhenSuccess_ShouldCommitAndReturnValue()
    {
        var fakeManager = new FakeTransactionManager();

        Result<int> result = await fakeManager.ExecuteResultAsync(async context =>
        {
            context.Should().NotBeNull();
            await Task.Yield();
            return Result<int>.Success(100);
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(100);
        fakeManager.StartedTransactions[0].CommitCount.Should().Be(1);
        fakeManager.StartedTransactions[0].RollbackCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteResultAsync_Generic_WithContext_WhenFailure_ShouldRollbackAndReturnError()
    {
        var fakeManager = new FakeTransactionManager();
        var expectedError = Error.NotFound("ENTITY_NOT_FOUND", "Entity was not found.");

        Result<string> result = await fakeManager.ExecuteResultAsync(async context =>
        {
            await Task.Yield();
            return Result<string>.Failure(expectedError);
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
        fakeManager.StartedTransactions[0].CommitCount.Should().Be(0);
        fakeManager.StartedTransactions[0].RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteResultAsync_NonGeneric_Parameterless_WhenSuccess_ShouldCommitTransaction()
    {
        var fakeManager = new FakeTransactionManager();

        ResultInstance result = await fakeManager.ExecuteResultAsync(async () =>
        {
            await Task.Yield();
            return ResultInstance.Success();
        });

        result.IsSuccess.Should().BeTrue();
        fakeManager.StartedTransactions.Should().HaveCount(1);
        fakeManager.StartedTransactions[0].CommitCount.Should().Be(1);
        fakeManager.StartedTransactions[0].RollbackCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteResultAsync_NonGeneric_Parameterless_WhenFailure_ShouldRollbackTransaction()
    {
        var fakeManager = new FakeTransactionManager();
        var expectedError = Error.Validation("FAIL", "Failed");

        ResultInstance result = await fakeManager.ExecuteResultAsync(async () =>
        {
            await Task.Yield();
            return ResultInstance.Failure(expectedError);
        });

        result.IsFailure.Should().BeTrue();
        fakeManager.StartedTransactions[0].CommitCount.Should().Be(0);
        fakeManager.StartedTransactions[0].RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteResultAsync_Generic_Parameterless_WhenSuccess_ShouldCommitAndReturnValue()
    {
        var fakeManager = new FakeTransactionManager();

        Result<string> result = await fakeManager.ExecuteResultAsync(async () =>
        {
            await Task.Yield();
            return Result<string>.Success("ok");
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
        fakeManager.StartedTransactions[0].CommitCount.Should().Be(1);
        fakeManager.StartedTransactions[0].RollbackCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteResultAsync_Generic_Parameterless_WhenFailure_ShouldRollbackAndReturnError()
    {
        var fakeManager = new FakeTransactionManager();
        var expectedError = Error.Conflict("CONFLICT", "Conflict error");

        Result<string> result = await fakeManager.ExecuteResultAsync(async () =>
        {
            await Task.Yield();
            return Result<string>.Failure(expectedError);
        });

        result.IsFailure.Should().BeTrue();
        fakeManager.StartedTransactions[0].CommitCount.Should().Be(0);
        fakeManager.StartedTransactions[0].RollbackCount.Should().Be(1);
    }
}
