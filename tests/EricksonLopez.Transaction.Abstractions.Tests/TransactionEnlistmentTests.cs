// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Transaction.Abstractions.Tests;

public sealed class TransactionEnlistmentTests
{
    private sealed class DefaultEnlistment : ITransactionEnlistment
    {
    }

    [Fact]
    public async Task DefaultInterfaceMethods_ShouldCompleteSuccessfully()
    {
        ITransactionEnlistment enlistment = new DefaultEnlistment();
        var context = Substitute.For<ITransactionContext>();
        var ex = new InvalidOperationException("Test fault");

        Func<Task> beforeCommit = () => enlistment.BeforeCommitAsync(context, CancellationToken.None);
        Func<Task> afterCommit = () => enlistment.AfterCommitAsync(context, CancellationToken.None);
        Func<Task> afterRollback = () => enlistment.AfterRollbackAsync(context, CancellationToken.None);
        Func<Task> onException = () => enlistment.OnExceptionAsync(context, ex, CancellationToken.None);

        await beforeCommit.Should().NotThrowAsync();
        await afterCommit.Should().NotThrowAsync();
        await afterRollback.Should().NotThrowAsync();
        await onException.Should().NotThrowAsync();
    }
}
