// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EricksonLopez.Transaction.Sqlite.Tests;

public sealed class SqliteErrorClassifierTests
{
    [Theory]
    [InlineData(5)] // SQLITE_BUSY
    [InlineData(6)] // SQLITE_LOCKED
    public void IsBusyOrLocked_WhenBusyOrLockedErrorCode_ShouldReturnTrue(int errorCode)
    {
        var ex = new SqliteException("SQLite busy or locked", errorCode);
        SqliteErrorClassifier.IsBusyOrLocked(ex).Should().BeTrue();
        SqliteErrorClassifier.IsBusyOrLocked(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsBusyOrLocked_WhenDifferentErrorCode_ShouldReturnFalse()
    {
        var ex = new SqliteException("Constraint error", 19);
        SqliteErrorClassifier.IsBusyOrLocked(ex).Should().BeFalse();
        SqliteErrorClassifier.IsBusyOrLocked(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void IsTransient_WhenBusyOrLocked_ShouldReturnTrue(int errorCode)
    {
        var ex = new SqliteException("SQLite busy", errorCode);
        SqliteErrorClassifier.IsTransient(ex).Should().BeTrue();
        SqliteErrorClassifier.IsTransient(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNonTransientSqliteException_ShouldReturnFalse()
    {
        var ex = new SqliteException("Constraint error", 19);
        SqliteErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenTimeoutException_ShouldReturnTrue()
    {
        var ex = new TimeoutException("Database timeout");
        SqliteErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedTransient_ShouldReturnTrue()
    {
        var ex = new InvalidOperationException("Outer wrapper", new TimeoutException("DB timeout"));
        SqliteErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("Outer wrapper", new ArgumentException("Non transient inner"));
        SqliteErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNoInnerExceptionAndNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("No inner exception");
        ex.InnerException.Should().BeNull();
        SqliteErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNull_ShouldReturnFalse()
    {
        SqliteErrorClassifier.IsTransient(null).Should().BeFalse();
    }
}
