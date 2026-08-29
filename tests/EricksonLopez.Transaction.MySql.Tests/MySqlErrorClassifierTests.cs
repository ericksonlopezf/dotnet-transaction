// Copyright © Erickson Lopez. MIT License.
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using MySqlConnector;
using Xunit;

namespace EricksonLopez.Transaction.MySql.Tests;

public sealed class MySqlErrorClassifierTests
{
    private static MySqlException CreateMySqlException(int errorNumber, bool isTransient = false)
    {
        var ex = (MySqlException)RuntimeHelpers.GetUninitializedObject(typeof(MySqlException));
        var numberField = typeof(MySqlException).GetField("m_number", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(MySqlException).GetField("_number", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(MySqlException).GetField("<Number>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        numberField?.SetValue(ex, errorNumber);

        var isTransientField = typeof(MySqlException).GetField("m_isTransient", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(MySqlException).GetField("_isTransient", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(MySqlException).GetField("<IsTransient>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        isTransientField?.SetValue(ex, isTransient);

        return ex;
    }

    [Fact]
    public void IsDeadlock_WhenDeadlock1213_ShouldReturnTrue()
    {
        var ex = CreateMySqlException(1213);
        MySqlErrorClassifier.IsDeadlock(ex).Should().BeTrue();
        MySqlErrorClassifier.IsDeadlock(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsDeadlock_WhenDifferentNumber_ShouldReturnFalse()
    {
        var ex = CreateMySqlException(1062);
        MySqlErrorClassifier.IsDeadlock(ex).Should().BeFalse();
        MySqlErrorClassifier.IsDeadlock(null).Should().BeFalse();
    }

    [Fact]
    public void IsLockWaitTimeout_WhenTimeout1205_ShouldReturnTrue()
    {
        var ex = CreateMySqlException(1205);
        MySqlErrorClassifier.IsLockWaitTimeout(ex).Should().BeTrue();
        MySqlErrorClassifier.IsLockWaitTimeout(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsLockWaitTimeout_WhenDifferentNumber_ShouldReturnFalse()
    {
        var ex = CreateMySqlException(1062);
        MySqlErrorClassifier.IsLockWaitTimeout(ex).Should().BeFalse();
        MySqlErrorClassifier.IsLockWaitTimeout(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(1213)]
    [InlineData(1205)]
    [InlineData(1053)]
    [InlineData(2003)]
    [InlineData(2013)]
    public void IsTransient_WhenTransientNumber_ShouldReturnTrue(int number)
    {
        var ex = CreateMySqlException(number);
        MySqlErrorClassifier.IsTransient(ex).Should().BeTrue();
        MySqlErrorClassifier.IsTransient(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenIsTransientPropertyTrue_ShouldReturnTrue()
    {
        var ex = CreateMySqlException(9999, isTransient: true);
        if (ex.IsTransient)
        {
            MySqlErrorClassifier.IsTransient(ex).Should().BeTrue();
        }
    }

    [Fact]
    public void IsTransient_WhenNonTransientMySqlException_ShouldReturnFalse()
    {
        var ex = CreateMySqlException(1062, isTransient: false);
        if (!ex.IsTransient)
        {
            MySqlErrorClassifier.IsTransient(ex).Should().BeFalse();
        }
    }

    [Fact]
    public void IsTransient_WhenTimeoutException_ShouldReturnTrue()
    {
        var ex = new TimeoutException("Database timeout");
        MySqlErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedTransient_ShouldReturnTrue()
    {
        var ex = new InvalidOperationException("Outer wrapper", new TimeoutException("DB timeout"));
        MySqlErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("Outer wrapper", new ArgumentException("Non transient inner"));
        MySqlErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNoInnerExceptionAndNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("No inner exception");
        ex.InnerException.Should().BeNull();
        MySqlErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNull_ShouldReturnFalse()
    {
        MySqlErrorClassifier.IsTransient(null).Should().BeFalse();
    }
}
