// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Transaction.Exceptions;
using Xunit;

namespace EricksonLopez.Transaction.Abstractions.Tests;

public sealed class TransactionExceptionsTests
{
    [Fact]
    public void TransactionException_DefaultConstructor_ShouldInitializeCorrectly()
    {
        var ex = new TransactionException();

        ex.Message.Should().NotBeNullOrWhiteSpace();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void TransactionException_MessageConstructor_ShouldSetMessage()
    {
        const string expectedMessage = "General transaction failure occurred.";
        var ex = new TransactionException(expectedMessage);

        ex.Message.Should().Be(expectedMessage);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void TransactionException_MessageAndInnerConstructor_ShouldSetBoth()
    {
        const string expectedMessage = "General transaction failure occurred.";
        var inner = new InvalidOperationException("Inner failure");
        var ex = new TransactionException(expectedMessage, inner);

        ex.Message.Should().Be(expectedMessage);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void TransactionCommitException_MessageConstructor_ShouldDefaultIsAmbiguousToFalse()
    {
        const string message = "Commit failed on database engine.";
        var ex = new TransactionCommitException(message);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeNull();
        ex.IsAmbiguous.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TransactionCommitException_MessageAndAmbiguityConstructor_ShouldSetProperties(bool isAmbiguous)
    {
        const string message = "Commit failed on database engine.";
        var ex = new TransactionCommitException(message, isAmbiguous: isAmbiguous);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeNull();
        ex.IsAmbiguous.Should().Be(isAmbiguous);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TransactionCommitException_MessageInnerAndAmbiguityConstructor_ShouldSetProperties(bool isAmbiguous)
    {
        const string message = "Commit failed due to network disruption.";
        var inner = new TimeoutException("Database connection timed out.");
        var ex = new TransactionCommitException(message, inner, isAmbiguous: isAmbiguous);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.IsAmbiguous.Should().Be(isAmbiguous);
    }

    [Fact]
    public void TransactionRollbackException_MessageConstructor_ShouldSetMessage()
    {
        const string message = "Rollback failed during transaction teardown.";
        var ex = new TransactionRollbackException(message);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void TransactionRollbackException_MessageAndInnerConstructor_ShouldSetBoth()
    {
        const string message = "Rollback failed during transaction teardown.";
        var inner = new InvalidOperationException("Connection closed.");
        var ex = new TransactionRollbackException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void TransactionStateException_StateAndOperationConstructor_ShouldFormatMessageCorrectly()
    {
        var ex = new TransactionStateException(TransactionState.Committed, "Rollback");

        ex.ActualState.Should().Be(TransactionState.Committed);
        ex.AttemptedOperation.Should().Be("Rollback");
        ex.Message.Should().Be("Cannot execute 'Rollback' because the transaction is in the 'Committed' state.");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void TransactionStateException_CustomMessageAndInnerConstructor_ShouldSetProperties()
    {
        const string message = "Custom state transition error.";
        var inner = new InvalidOperationException("Driver state mismatch.");
        var ex = new TransactionStateException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.AttemptedOperation.Should().BeNull();
        ex.ActualState.Should().Be(TransactionState.Created);
    }

    [Fact]
    public void TransactionTimeoutException_TimeoutConstructor_ShouldFormatMessageCorrectly()
    {
        var timeout = TimeSpan.FromMilliseconds(5000);
        var ex = new TransactionTimeoutException(timeout);

        ex.Timeout.Should().Be(timeout);
        ex.Message.Should().Be("The transaction exceeded its configured timeout of 5000ms and was aborted.");
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void TransactionTimeoutException_CustomMessageInnerAndTimeoutConstructor_ShouldSetProperties()
    {
        const string message = "Custom timeout abort message.";
        var inner = new TimeoutException("Socket timed out.");
        var timeout = TimeSpan.FromSeconds(30);
        var ex = new TransactionTimeoutException(message, inner, timeout);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.Timeout.Should().Be(timeout);
    }
}
