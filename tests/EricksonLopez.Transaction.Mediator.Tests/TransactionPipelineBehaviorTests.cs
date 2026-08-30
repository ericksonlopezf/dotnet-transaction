using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Result;
using NSubstitute;
using Xunit;
using ResultInstance = EricksonLopez.Result.Result;

namespace EricksonLopez.Transaction.Mediator.Tests;

public sealed class TransactionPipelineBehaviorTests
{
    private readonly ITransactionManager _transactionManager = Substitute.For<ITransactionManager>();
    private readonly ITransaction _transaction = Substitute.For<ITransaction>();

    public TransactionPipelineBehaviorTests()
    {
        _transactionManager.BeginAsync(Arg.Any<TransactionOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_transaction));
    }

    [Fact]
    public async Task Handle_NonTransactionalRequest_DoesNotBeginTransaction()
    {
        var behavior = new TransactionPipelineBehavior<TestNonTransactionalRequest, Result<string>>(_transactionManager);
        var request = new TestNonTransactionalRequest("GetReport");
        var next = new TestNextContinuation<Result<string>>(() => ValueTask.FromResult(Result<string>.Success("ReportData")));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ReportData");
        await _transactionManager.DidNotReceive().BeginAsync(Arg.Any<TransactionOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TransactionalSuccess_BeginsAndCommitsTransaction()
    {
        var behavior = new TransactionPipelineBehavior<TestTransactionalCommand, ResultInstance>(_transactionManager);
        var request = new TestTransactionalCommand("CreateInvoice");
        var next = new TestNextContinuation<ResultInstance>(() => ValueTask.FromResult(ResultInstance.Success()));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _transactionManager.Received(1).BeginAsync(Arg.Any<TransactionOptions?>(), Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _transaction.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TransactionalFunctionalFailure_BeginsAndRollsBackTransaction()
    {
        var behavior = new TransactionPipelineBehavior<TestTransactionalCommand, ResultInstance>(_transactionManager);
        var request = new TestTransactionalCommand("CreateInvoice");
        var next = new TestNextContinuation<ResultInstance>(() => ValueTask.FromResult(ResultInstance.Failure(Error.Validation("Error.Validation", "Invalid amount"))));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _transactionManager.Received(1).BeginAsync(Arg.Any<TransactionOptions?>(), Arg.Any<CancellationToken>());
        await _transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnhandledException_RollsBackAndRethrows()
    {
        var behavior = new TransactionPipelineBehavior<TestTransactionalCommand, ResultInstance>(_transactionManager);
        var request = new TestTransactionalCommand("CreateInvoice");
        var next = new TestNextContinuation<ResultInstance>(() => throw new InvalidOperationException("DB deadlock"));

        var act = async () => await behavior.Handle(request, next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB deadlock");
        await _transactionManager.Received(1).BeginAsync(Arg.Any<TransactionOptions?>(), Arg.Any<CancellationToken>());
        await _transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
