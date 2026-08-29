// Copyright © Erickson Lopez. MIT License.
using System.Data;
using AwesomeAssertions;
using EricksonLopez.Transaction.Internal;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class IsolationLevelConverterTests
{
    [Theory]
    [InlineData(TransactionIsolationLevel.ReadUncommitted, IsolationLevel.ReadUncommitted)]
    [InlineData(TransactionIsolationLevel.ReadCommitted, IsolationLevel.ReadCommitted)]
    [InlineData(TransactionIsolationLevel.RepeatableRead, IsolationLevel.RepeatableRead)]
    [InlineData(TransactionIsolationLevel.Serializable, IsolationLevel.Serializable)]
    [InlineData(TransactionIsolationLevel.Snapshot, IsolationLevel.Snapshot)]
    [InlineData(TransactionIsolationLevel.Unspecified, IsolationLevel.Unspecified)]
    [InlineData((TransactionIsolationLevel)999, IsolationLevel.Unspecified)]
    public void ToSystemIsolationLevel_ShouldMapAccurately(TransactionIsolationLevel input, IsolationLevel expected)
    {
        IsolationLevel result = IsolationLevelConverter.ToSystemIsolationLevel(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(IsolationLevel.ReadUncommitted, TransactionIsolationLevel.ReadUncommitted)]
    [InlineData(IsolationLevel.ReadCommitted, TransactionIsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead, TransactionIsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable, TransactionIsolationLevel.Serializable)]
    [InlineData(IsolationLevel.Snapshot, TransactionIsolationLevel.Snapshot)]
    [InlineData(IsolationLevel.Unspecified, TransactionIsolationLevel.Unspecified)]
    [InlineData(IsolationLevel.Chaos, TransactionIsolationLevel.Unspecified)]
    [InlineData((IsolationLevel)999, TransactionIsolationLevel.Unspecified)]
    public void ToFrameworkIsolationLevel_ShouldMapAccurately(IsolationLevel input, TransactionIsolationLevel expected)
    {
        TransactionIsolationLevel result = IsolationLevelConverter.ToFrameworkIsolationLevel(input);
        result.Should().Be(expected);
    }
}
