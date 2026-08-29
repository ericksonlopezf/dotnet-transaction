// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.Transaction.Abstractions.Tests;

public sealed class TransactionOptionsTests
{
    [Fact]
    public void Default_ShouldHaveReadCommittedAndUseSavepoint()
    {
        var options = TransactionOptions.Default;

        options.IsolationLevel.Should().Be(TransactionIsolationLevel.ReadCommitted);
        options.NestedBehavior.Should().Be(NestedTransactionBehavior.UseSavepoint);
        options.Timeout.Should().BeNull();
        options.ReadOnly.Should().BeFalse();
        options.TransactionName.Should().BeNull();
    }

    [Fact]
    public void Serializable_ShouldHaveSerializableIsolationLevel()
    {
        var options = TransactionOptions.Serializable;

        options.IsolationLevel.Should().Be(TransactionIsolationLevel.Serializable);
        options.NestedBehavior.Should().Be(NestedTransactionBehavior.UseSavepoint);
        options.Timeout.Should().BeNull();
        options.ReadOnly.Should().BeFalse();
        options.TransactionName.Should().BeNull();
    }

    [Fact]
    public void ReadOnlyMode_ShouldBeConfiguredAsReadOnly()
    {
        var options = TransactionOptions.ReadOnlyMode;

        options.ReadOnly.Should().BeTrue();
        options.IsolationLevel.Should().Be(TransactionIsolationLevel.ReadCommitted);
        options.NestedBehavior.Should().Be(NestedTransactionBehavior.UseSavepoint);
        options.Timeout.Should().BeNull();
        options.TransactionName.Should().BeNull();
    }

    [Fact]
    public void WithTimeout_ShouldConfigureTimeoutCorrectly()
    {
        var timeout = TimeSpan.FromSeconds(15);
        var options = TransactionOptions.WithTimeout(timeout);

        options.Timeout.Should().Be(timeout);
        options.IsolationLevel.Should().Be(TransactionIsolationLevel.ReadCommitted);
        options.NestedBehavior.Should().Be(NestedTransactionBehavior.UseSavepoint);
        options.ReadOnly.Should().BeFalse();
        options.TransactionName.Should().BeNull();
    }

    [Fact]
    public void CustomInitialization_ShouldPreserveAllProvidedValues()
    {
        var timeout = TimeSpan.FromMinutes(2);
        var options = new TransactionOptions
        {
            IsolationLevel = TransactionIsolationLevel.Snapshot,
            NestedBehavior = NestedTransactionBehavior.JoinExisting,
            ReadOnly = true,
            Timeout = timeout,
            TransactionName = "CustomTx"
        };

        options.IsolationLevel.Should().Be(TransactionIsolationLevel.Snapshot);
        options.NestedBehavior.Should().Be(NestedTransactionBehavior.JoinExisting);
        options.ReadOnly.Should().BeTrue();
        options.Timeout.Should().Be(timeout);
        options.TransactionName.Should().Be("CustomTx");
    }

    [Fact]
    public void WithExpression_ShouldCloneAndApplyModifications()
    {
        var original = TransactionOptions.Default;
        var modified = original with
        {
            TransactionName = "ClonedTx",
            IsolationLevel = TransactionIsolationLevel.RepeatableRead,
            NestedBehavior = NestedTransactionBehavior.RequireNew
        };

        original.TransactionName.Should().BeNull();
        original.IsolationLevel.Should().Be(TransactionIsolationLevel.ReadCommitted);
        original.NestedBehavior.Should().Be(NestedTransactionBehavior.UseSavepoint);

        modified.TransactionName.Should().Be("ClonedTx");
        modified.IsolationLevel.Should().Be(TransactionIsolationLevel.RepeatableRead);
        modified.NestedBehavior.Should().Be(NestedTransactionBehavior.RequireNew);
        modified.ReadOnly.Should().BeFalse();
        modified.Timeout.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_ShouldBeValueBased()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var first = new TransactionOptions
        {
            IsolationLevel = TransactionIsolationLevel.ReadCommitted,
            NestedBehavior = NestedTransactionBehavior.UseSavepoint,
            ReadOnly = false,
            Timeout = timeout,
            TransactionName = "TxA"
        };

        var second = new TransactionOptions
        {
            IsolationLevel = TransactionIsolationLevel.ReadCommitted,
            NestedBehavior = NestedTransactionBehavior.UseSavepoint,
            ReadOnly = false,
            Timeout = timeout,
            TransactionName = "TxA"
        };

        var different = first with { TransactionName = "TxB" };

        (first == second).Should().BeTrue();
        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());

        (first == different).Should().BeFalse();
        first.Equals(different).Should().BeFalse();
    }
}
