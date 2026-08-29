// Copyright © Erickson Lopez. MIT License.
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Npgsql;
using Xunit;

namespace EricksonLopez.Transaction.PostgreSql.Tests;

public sealed class PostgreSqlErrorClassifierTests
{
    private static PostgresException CreatePostgresException(string sqlState)
    {
        var ctor = typeof(PostgresException).GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(string), typeof(string), typeof(string), typeof(string) },
            null);

        if (ctor is not null)
        {
            return (PostgresException)ctor.Invoke(new object[] { "Error message", "ERROR", "ERROR", sqlState });
        }

        var ex = (PostgresException)RuntimeHelpers.GetUninitializedObject(typeof(PostgresException));
        var backingField = typeof(PostgresException).GetField("<SqlState>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (backingField is not null)
        {
            backingField.SetValue(ex, sqlState);
        }
        return ex;
    }

    private static NpgsqlException CreateNpgsqlException(bool isTransient)
    {
        var ex = (NpgsqlException)RuntimeHelpers.GetUninitializedObject(typeof(NpgsqlException));
        var backingField = typeof(NpgsqlException).GetField("<IsTransient>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (backingField is not null)
        {
            backingField.SetValue(ex, isTransient);
        }
        return ex;
    }

    [Fact]
    public void Constants_ShouldMatchStandardPostgreSqlSqlStates()
    {
        PostgreSqlErrorClassifier.SerializationFailureSqlState.Should().Be("40001");
        PostgreSqlErrorClassifier.DeadlockDetectedSqlState.Should().Be("40P01");
        PostgreSqlErrorClassifier.InFailedSqlTransactionSqlState.Should().Be("25P02");
        PostgreSqlErrorClassifier.QueryCanceledSqlState.Should().Be("57014");
        PostgreSqlErrorClassifier.AdminShutdownSqlState.Should().Be("57P01");
        PostgreSqlErrorClassifier.CrashShutdownSqlState.Should().Be("57P02");
        PostgreSqlErrorClassifier.CannotConnectNowSqlState.Should().Be("57P03");
        PostgreSqlErrorClassifier.ConnectionFailureSqlState.Should().Be("08006");
    }

    [Fact]
    public void IsDeadlock_WhenSqlStateMatches_ShouldReturnTrue()
    {
        var ex = CreatePostgresException("40P01");
        PostgreSqlErrorClassifier.IsDeadlock(ex).Should().BeTrue();
        PostgreSqlErrorClassifier.IsDeadlock(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsDeadlock_WhenSqlStateDoesNotMatch_ShouldReturnFalse()
    {
        var ex = CreatePostgresException("23505");
        PostgreSqlErrorClassifier.IsDeadlock(ex).Should().BeFalse();
        PostgreSqlErrorClassifier.IsDeadlock(null).Should().BeFalse();
    }

    [Fact]
    public void IsSerializationFailure_WhenSqlStateMatches_ShouldReturnTrue()
    {
        var ex = CreatePostgresException("40001");
        PostgreSqlErrorClassifier.IsSerializationFailure(ex).Should().BeTrue();
        PostgreSqlErrorClassifier.IsSerializationFailure(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsSerializationFailure_WhenSqlStateDoesNotMatch_ShouldReturnFalse()
    {
        var ex = CreatePostgresException("23505");
        PostgreSqlErrorClassifier.IsSerializationFailure(ex).Should().BeFalse();
        PostgreSqlErrorClassifier.IsSerializationFailure(null).Should().BeFalse();
    }

    [Fact]
    public void IsInFailedTransaction_WhenSqlStateMatches_ShouldReturnTrue()
    {
        var ex = CreatePostgresException("25P02");
        PostgreSqlErrorClassifier.IsInFailedTransaction(ex).Should().BeTrue();
        PostgreSqlErrorClassifier.IsInFailedTransaction(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsInFailedTransaction_WhenSqlStateDoesNotMatch_ShouldReturnFalse()
    {
        var ex = CreatePostgresException("23505");
        PostgreSqlErrorClassifier.IsInFailedTransaction(ex).Should().BeFalse();
        PostgreSqlErrorClassifier.IsInFailedTransaction(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("40001")]
    [InlineData("40P01")]
    [InlineData("08006")]
    [InlineData("57P01")]
    [InlineData("57P02")]
    [InlineData("57P03")]
    public void IsTransient_WhenTransientSqlState_ShouldReturnTrue(string sqlState)
    {
        var ex = CreatePostgresException(sqlState);
        PostgreSqlErrorClassifier.IsTransient(ex).Should().BeTrue();
        PostgreSqlErrorClassifier.IsTransient(new InvalidOperationException("Wrapper", ex)).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNonTransientPostgresException_ShouldReturnFalse()
    {
        var ex = CreatePostgresException("23505");
        PostgreSqlErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNpgsqlExceptionWithIsTransient_ShouldReturnTrue()
    {
        var npgEx = new NpgsqlException("Connection timeout", new TimeoutException());
        npgEx.IsTransient.Should().BeTrue();
        PostgreSqlErrorClassifier.IsTransient(npgEx).Should().BeTrue();

        var nonTransientNpgEx = new NpgsqlException("General error");
        nonTransientNpgEx.IsTransient.Should().BeFalse();
        PostgreSqlErrorClassifier.IsTransient(nonTransientNpgEx).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenTimeoutException_ShouldReturnTrue()
    {
        var ex = new TimeoutException("Database query timeout");
        PostgreSqlErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedTransient_ShouldReturnTrue()
    {
        var ex = new InvalidOperationException("Outer wrapper", new TimeoutException("DB timeout"));
        PostgreSqlErrorClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_WhenNestedNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("Outer wrapper", new ArgumentException("Non transient inner"));
        PostgreSqlErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNoInnerExceptionAndNonTransient_ShouldReturnFalse()
    {
        var ex = new InvalidOperationException("No inner exception");
        ex.InnerException.Should().BeNull();
        PostgreSqlErrorClassifier.IsTransient(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNull_ShouldReturnFalse()
    {
        PostgreSqlErrorClassifier.IsTransient(null).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_WhenNonTransientException_ShouldReturnFalse()
    {
        var ex = new ArgumentException("Invalid argument value");
        PostgreSqlErrorClassifier.IsTransient(ex).Should().BeFalse();
    }
}
