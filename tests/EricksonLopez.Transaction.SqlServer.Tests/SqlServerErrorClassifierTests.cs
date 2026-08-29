// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace EricksonLopez.Transaction.SqlServer.Tests;

public sealed class SqlServerErrorClassifierTests
{
    private static SqlException CreateSqlException(int errorNumber)
    {
        var ex = (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));
        var errorCollection = (SqlErrorCollection)RuntimeHelpers.GetUninitializedObject(typeof(SqlErrorCollection));
        var error = (SqlError)RuntimeHelpers.GetUninitializedObject(typeof(SqlError));

        typeof(SqlError).GetField("_number", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(error, errorNumber);
        typeof(SqlError).GetField("number", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(error, errorNumber);

        var listField = typeof(SqlErrorCollection).GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(SqlErrorCollection).GetField("errors", BindingFlags.NonPublic | BindingFlags.Instance);

        if (listField is not null)
        {
            var list = (IList)Activator.CreateInstance(listField.FieldType)!;
            list.Add(error);
            listField.SetValue(errorCollection, list);
        }

        var errorsField = typeof(SqlException).GetField("_errors", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(SqlException).GetField("errors", BindingFlags.NonPublic | BindingFlags.Instance);
        errorsField?.SetValue(ex, errorCollection);

        var numberField = typeof(SqlException).GetField("_number", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(SqlException).GetField("number", BindingFlags.NonPublic | BindingFlags.Instance);
        numberField?.SetValue(ex, errorNumber);

        return ex;
    }

    [Fact]
    public void IsDeadlock_WhenDeadlockNumber1205_ShouldReturnTrue()
    {
        var ex = CreateSqlException(1205);
        SqlServerErrorClassifier.IsDeadlock(ex).Should().BeTrue();
        SqlServerErrorClassifier.IsDeadlock(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsDeadlock_WhenDifferentNumber_ShouldReturnFalse()
    {
        var ex = CreateSqlException(2601);
        SqlServerErrorClassifier.IsDeadlock(ex).Should().BeFalse();
        SqlServerErrorClassifier.IsDeadlock(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(3960)]
    [InlineData(3961)]
    public void IsSerializationFailure_WhenConflictNumbers_ShouldReturnTrue(int number)
    {
        var ex = CreateSqlException(number);
        SqlServerErrorClassifier.IsSerializationFailure(ex).Should().BeTrue();
        SqlServerErrorClassifier.IsSerializationFailure(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsSerializationFailure_WhenDifferentNumber_ShouldReturnFalse()
    {
        var ex = CreateSqlException(2601);
        SqlServerErrorClassifier.IsSerializationFailure(ex).Should().BeFalse();
        SqlServerErrorClassifier.IsSerializationFailure(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(3960)]
    [InlineData(3961)]
    [InlineData(-2)]
    [InlineData(4060)]
    [InlineData(40197)]
    [InlineData(40501)]
    [InlineData(40613)]
    [InlineData(49918)]
    [InlineData(49919)]
    [InlineData(49920)]
    public void IsTransient_WhenTransientNumber_ShouldReturnTrue(int number)
    {
        var ex = CreateSqlException(number);
        SqlServerErrorClassifier.IsTransient(ex).Should().BeTrue();
        SqlServerErrorClassifier.IsTransient(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNonTransientSqlException_ShouldReturnFalse()
    {
        var ex = CreateSqlException(2601);
        SqlServerErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenTimeoutException_ShouldReturnTrue()
    {
        var ex = new TimeoutException("Timeout occurred");
        SqlServerErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedTransient_ShouldReturnTrue()
    {
        var ex = new InvalidOperationException("Outer wrapper", new TimeoutException("DB timeout"));
        SqlServerErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("Outer wrapper", new ArgumentException("Non transient inner"));
        SqlServerErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNoInnerExceptionAndNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("No inner exception");
        ex.InnerException.Should().BeNull();
        SqlServerErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNull_ShouldReturnFalse()
    {
        SqlServerErrorClassifier.IsTransient(null).Should().BeFalse();
    }
}
