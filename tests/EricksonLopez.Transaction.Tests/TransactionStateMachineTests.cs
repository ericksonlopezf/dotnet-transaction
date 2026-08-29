// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Transaction.Exceptions;
using EricksonLopez.Transaction.Internal;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class TransactionStateMachineTests
{
    [Fact]
    public void InitialState_ShouldBeCreatedByDefault()
    {
        var machine = new TransactionStateMachine();
        machine.CurrentState.Should().Be(TransactionState.Created);
    }

    [Fact]
    public void InitialState_WhenExplicitlySet_ShouldMatch()
    {
        var machine = new TransactionStateMachine(TransactionState.Active);
        machine.CurrentState.Should().Be(TransactionState.Active);
    }

    [Fact]
    public void ValidTransitions_FromCreatedToActive_ShouldSucceed()
    {
        var machine = new TransactionStateMachine(TransactionState.Created);
        machine.TransitionToActive();
        machine.CurrentState.Should().Be(TransactionState.Active);
    }

    [Theory]
    [InlineData(TransactionState.Active)]
    [InlineData(TransactionState.Committed)]
    [InlineData(TransactionState.RolledBack)]
    [InlineData(TransactionState.Failed)]
    [InlineData(TransactionState.Disposed)]
    public void TransitionToActive_FromNonCreatedState_ShouldThrowTransactionStateException(TransactionState state)
    {
        var machine = new TransactionStateMachine(state);
        Action act = () => machine.TransitionToActive();

        act.Should().Throw<TransactionStateException>()
            .Where(e => e.ActualState == state && e.AttemptedOperation == "Begin");
    }

    [Fact]
    public void ValidTransitions_FromActiveToCommitted_ShouldSucceed()
    {
        var machine = new TransactionStateMachine(TransactionState.Active);
        machine.TransitionToCommitted();
        machine.CurrentState.Should().Be(TransactionState.Committed);
    }

    [Theory]
    [InlineData(TransactionState.Created)]
    [InlineData(TransactionState.Committed)]
    [InlineData(TransactionState.RolledBack)]
    [InlineData(TransactionState.Failed)]
    [InlineData(TransactionState.Disposed)]
    public void TransitionToCommitted_FromNonActiveState_ShouldThrowTransactionStateException(TransactionState state)
    {
        var machine = new TransactionStateMachine(state);
        Action act = () => machine.TransitionToCommitted();

        act.Should().Throw<TransactionStateException>()
            .Where(e => e.ActualState == state && e.AttemptedOperation == "Commit");
    }

    [Theory]
    [InlineData(TransactionState.Created)]
    [InlineData(TransactionState.Active)]
    [InlineData(TransactionState.Failed)]
    public void TransitionToRolledBack_FromValidStates_ShouldSucceed(TransactionState state)
    {
        var machine = new TransactionStateMachine(state);
        machine.TransitionToRolledBack();
        machine.CurrentState.Should().Be(TransactionState.RolledBack);
    }

    [Theory]
    [InlineData(TransactionState.RolledBack)]
    [InlineData(TransactionState.Disposed)]
    public void TransitionToRolledBack_WhenAlreadyRolledBackOrDisposed_ShouldBeIdempotentNoOp(TransactionState state)
    {
        var machine = new TransactionStateMachine(state);
        machine.TransitionToRolledBack();
        machine.CurrentState.Should().Be(state);
    }

    [Fact]
    public void TransitionToRolledBack_FromCommittedState_ShouldThrowTransactionStateException()
    {
        var machine = new TransactionStateMachine(TransactionState.Committed);
        Action act = () => machine.TransitionToRolledBack();

        act.Should().Throw<TransactionStateException>()
            .Where(e => e.ActualState == TransactionState.Committed && e.AttemptedOperation == "Rollback");
    }

    [Theory]
    [InlineData(TransactionState.Created)]
    [InlineData(TransactionState.Active)]
    [InlineData(TransactionState.Committed)]
    public void TransitionToFailed_FromCreatedActiveOrCommitted_ShouldSucceed(TransactionState state)
    {
        var machine = new TransactionStateMachine(state);
        machine.TransitionToFailed();
        machine.CurrentState.Should().Be(TransactionState.Failed);
    }

    [Theory]
    [InlineData(TransactionState.Failed)]
    [InlineData(TransactionState.RolledBack)]
    [InlineData(TransactionState.Disposed)]
    public void TransitionToFailed_WhenAlreadyFailedRolledBackOrDisposed_ShouldBeIdempotentNoOp(TransactionState state)
    {
        var machine = new TransactionStateMachine(state);
        machine.TransitionToFailed();
        machine.CurrentState.Should().Be(state);
    }

    [Fact]
    public void TransitionToDisposed_FromAnyState_ShouldSucceed()
    {
        var machine = new TransactionStateMachine(TransactionState.Active);
        machine.TransitionToDisposed();
        machine.CurrentState.Should().Be(TransactionState.Disposed);
    }

    [Fact]
    public void ConcurrentTransitions_ShouldRemainConsistent()
    {
        for (int i = 0; i < 50; i++)
        {
            var machine = new TransactionStateMachine(TransactionState.Active);
            Parallel.Invoke(
                () => machine.TransitionToRolledBack(),
                () => machine.TransitionToFailed(),
                () => machine.TransitionToDisposed());

            machine.CurrentState.Should().BeOneOf(TransactionState.RolledBack, TransactionState.Failed, TransactionState.Disposed);
        }
    }
}
