// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Result;
using Microsoft.Extensions.DependencyInjection;
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
    public void Constructor_NullTransactionManager_ThrowsArgumentNullException()
    {
        var act = () => new TransactionPipelineBehavior<TestTransactionalCommand, ResultInstance>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("transactionManager");
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

    [Fact]
    public async Task Handle_RollbackThrowsDuringExceptionHandling_ThrowsAggregateException()
    {
        var primaryEx = new InvalidOperationException("Primary error");
        var rollbackEx = new InvalidOperationException("Rollback error");
        _transaction.RollbackAsync(Arg.Any<CancellationToken>()).Returns<Task>(_ => throw rollbackEx);

        var behavior = new TransactionPipelineBehavior<TestTransactionalCommand, ResultInstance>(_transactionManager);
        var request = new TestTransactionalCommand("CreateInvoice");
        var next = new TestNextContinuation<ResultInstance>(() => throw primaryEx);

        var act = async () => await behavior.Handle(request, next, CancellationToken.None);

        var agg = await act.Should().ThrowAsync<AggregateException>();
        agg.Which.Message.Should().Contain("Transaction rollback failed following an unhandled command execution error.");
        agg.Which.InnerExceptions.Should().Contain(primaryEx);
        agg.Which.InnerExceptions.Should().Contain(rollbackEx);
    }

    [Fact]
    public async Task Handle_WithEnlistments_EnlistsAllParticipantsIntoContext()
    {
        var context = Substitute.For<ITransactionContext>();
        _transaction.Context.Returns(context);

        var enlistment1 = Substitute.For<ITransactionEnlistment>();
        var enlistment2 = Substitute.For<ITransactionEnlistment>();
        var enlistments = new[] { enlistment1, enlistment2 };

        var behavior = new TransactionPipelineBehavior<TestTransactionalCommand, ResultInstance>(_transactionManager, enlistments);
        var request = new TestTransactionalCommand("CreateInvoice");
        var next = new TestNextContinuation<ResultInstance>(() => ValueTask.FromResult(ResultInstance.Success()));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Received(1).Enlist(enlistment1);
        context.Received(1).Enlist(enlistment2);
    }

    [Fact]
    public async Task Handle_WithConfigurableOptions_PassesOptionsToBeginAsync()
    {
        var options = new TransactionOptions
        {
            IsolationLevel = TransactionIsolationLevel.Serializable,
            Timeout = TimeSpan.FromSeconds(30),
            NestedBehavior = NestedTransactionBehavior.RequireNew
        };
        var behavior = new TransactionPipelineBehavior<TestConfigurableCommand, ResultInstance>(_transactionManager);
        var request = new TestConfigurableCommand("CreateInvoice", options);
        var next = new TestNextContinuation<ResultInstance>(() => ValueTask.FromResult(ResultInstance.Success()));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _transactionManager.Received(1).BeginAsync(options, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNestedBehaviorSuppress_DoesNotEnlistParticipants()
    {
        var context = Substitute.For<ITransactionContext>();
        _transaction.Context.Returns(context);
        var enlistment = Substitute.For<ITransactionEnlistment>();
        var options = new TransactionOptions
        {
            IsolationLevel = TransactionIsolationLevel.ReadCommitted,
            Timeout = TimeSpan.FromSeconds(30),
            NestedBehavior = NestedTransactionBehavior.Suppress
        };
        var behavior = new TransactionPipelineBehavior<TestConfigurableCommand, ResultInstance>(_transactionManager, [enlistment]);
        var request = new TestConfigurableCommand("CreateInvoice", options);
        var next = new TestNextContinuation<ResultInstance>(() => ValueTask.FromResult(ResultInstance.Success()));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.DidNotReceive().Enlist(Arg.Any<ITransactionEnlistment>());
    }

    [Fact]
    public async Task Handle_NonResultResponse_CommitsOnSuccess()
    {
        var behavior = new TransactionPipelineBehavior<TestCommandWithNonResultResponse, string>(_transactionManager);
        var request = new TestCommandWithNonResultResponse("CreateInvoice");
        var next = new TestNextContinuation<string>(() => ValueTask.FromResult("order-123"));

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.Should().Be("order-123");
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _transaction.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TransactionalAttribute_DefaultConstructor_SetsDefaultProperties()
    {
        var attr = new TransactionalAttribute();
        attr.IsolationLevel.Should().Be(TransactionIsolationLevel.ReadCommitted);
        attr.TimeoutSeconds.Should().Be(0);

        attr.IsolationLevel = TransactionIsolationLevel.Serializable;
        attr.TimeoutSeconds = 45;
        attr.IsolationLevel.Should().Be(TransactionIsolationLevel.Serializable);
        attr.TimeoutSeconds.Should().Be(45);
    }

    [Fact]
    public void TransactionalAttribute_ParameterizedConstructor_SetsIsolationLevel()
    {
        var attr = new TransactionalAttribute(TransactionIsolationLevel.RepeatableRead);
        attr.IsolationLevel.Should().Be(TransactionIsolationLevel.RepeatableRead);
        attr.TimeoutSeconds.Should().Be(0);
    }

    [Fact]
    public void AddTransactionPipelineBehavior_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var ex = Assert.Throws<ArgumentNullException>(() => services.AddTransactionPipelineBehavior());
        ex.ParamName.Should().Be("services");
        ex.StackTrace.Should().NotContain("AddTransient");
    }

    [Fact]
    public void AddTransactionPipelineBehavior_ValidServices_RegistersOpenGenericPipelineBehavior()
    {
        var services = new ServiceCollection();
        var returned = services.AddTransactionPipelineBehavior();

        returned.Should().BeSameAs(services);
        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IPipelineBehavior<,>));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be(typeof(TransactionPipelineBehavior<,>));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Transient);
    }
}
