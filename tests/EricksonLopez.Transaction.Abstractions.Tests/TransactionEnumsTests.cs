// Copyright © Erickson Lopez. MIT License.
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.Transaction.Abstractions.Tests;

public sealed class TransactionEnumsTests
{
    [Fact]
    public void NestedTransactionBehavior_ShouldDefineExpectedValues()
    {
        NestedTransactionBehavior.UseSavepoint.Should().BeDefined();
        NestedTransactionBehavior.RequireNew.Should().BeDefined();
        NestedTransactionBehavior.Suppress.Should().BeDefined();
        NestedTransactionBehavior.JoinExisting.Should().BeDefined();
    }

    [Fact]
    public void TransactionIsolationLevel_ShouldDefineExpectedValues()
    {
        TransactionIsolationLevel.Unspecified.Should().BeDefined();
        TransactionIsolationLevel.ReadUncommitted.Should().BeDefined();
        TransactionIsolationLevel.ReadCommitted.Should().BeDefined();
        TransactionIsolationLevel.RepeatableRead.Should().BeDefined();
        TransactionIsolationLevel.Serializable.Should().BeDefined();
        TransactionIsolationLevel.Snapshot.Should().BeDefined();
    }

    [Fact]
    public void TransactionState_ShouldDefineExpectedValues()
    {
        TransactionState.Created.Should().BeDefined();
        TransactionState.Active.Should().BeDefined();
        TransactionState.Committed.Should().BeDefined();
        TransactionState.RolledBack.Should().BeDefined();
        TransactionState.Failed.Should().BeDefined();
        TransactionState.Disposed.Should().BeDefined();
    }
}
