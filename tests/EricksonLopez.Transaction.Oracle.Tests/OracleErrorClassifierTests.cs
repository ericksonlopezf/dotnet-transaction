// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace EricksonLopez.Transaction.Oracle.Tests;

public sealed class OracleErrorClassifierTests
{
    private static OracleException CreateOracleException(int errorNumber)
    {
        var ex = (OracleException)RuntimeHelpers.GetUninitializedObject(typeof(OracleException));
        var errCol = (OracleErrorCollection)RuntimeHelpers.GetUninitializedObject(typeof(OracleErrorCollection));
        var err = (OracleError)RuntimeHelpers.GetUninitializedObject(typeof(OracleError));

        typeof(OracleError).GetField("m_number", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(err, errorNumber);

        typeof(ArrayList).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(errCol, new object[] { err });
        typeof(ArrayList).GetField("_size", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(errCol, 1);

        typeof(OracleException).GetField("m_errors", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(ex, errCol);

        return ex;
    }

    [Fact]
    public void IsDeadlock_WhenDeadlock60_ShouldReturnTrue()
    {
        var ex = CreateOracleException(60);
        OracleErrorClassifier.IsDeadlock(ex).Should().BeTrue();
        OracleErrorClassifier.IsDeadlock(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsDeadlock_WhenDifferentNumber_ShouldReturnFalse()
    {
        var ex = CreateOracleException(1);
        OracleErrorClassifier.IsDeadlock(ex).Should().BeFalse();
        OracleErrorClassifier.IsDeadlock(null).Should().BeFalse();
    }

    [Fact]
    public void IsSerializationFailure_WhenConflict8177_ShouldReturnTrue()
    {
        var ex = CreateOracleException(8177);
        OracleErrorClassifier.IsSerializationFailure(ex).Should().BeTrue();
        OracleErrorClassifier.IsSerializationFailure(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsSerializationFailure_WhenDifferentNumber_ShouldReturnFalse()
    {
        var ex = CreateOracleException(1);
        OracleErrorClassifier.IsSerializationFailure(ex).Should().BeFalse();
        OracleErrorClassifier.IsSerializationFailure(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(60)]
    [InlineData(8177)]
    [InlineData(12541)]
    [InlineData(12543)]
    [InlineData(12170)]
    [InlineData(12571)]
    [InlineData(3113)]
    [InlineData(3114)]
    public void IsTransient_WhenTransientNumber_ShouldReturnTrue(int number)
    {
        var ex = CreateOracleException(number);
        OracleErrorClassifier.IsTransient(ex).Should().BeTrue();
        OracleErrorClassifier.IsTransient(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNonTransientOracleException_ShouldReturnFalse()
    {
        var ex = CreateOracleException(1);
        OracleErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenTimeoutException_ShouldReturnTrue()
    {
        var ex = new TimeoutException("Database timeout");
        OracleErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedTransient_ShouldReturnTrue()
    {
        var ex = new InvalidOperationException("Outer wrapper", new TimeoutException("DB timeout"));
        OracleErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("Outer wrapper", new ArgumentException("Non transient inner"));
        OracleErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNoInnerExceptionAndNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("No inner exception");
        ex.InnerException.Should().BeNull();
        OracleErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNull_ShouldReturnFalse()
    {
        OracleErrorClassifier.IsTransient(null).Should().BeFalse();
    }
}
