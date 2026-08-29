// Copyright © Erickson Lopez. MIT License.
using System;
using Npgsql;

namespace EricksonLopez.Transaction.PostgreSql;

/// <summary>
/// Provides classification methods for PostgreSQL error codes, deadlocks, serialization conflicts, and transient failures.
/// </summary>
public static class PostgreSqlErrorClassifier
{
    /// <summary>Specifies PostgreSQL SQLSTATE 40001 (serialization failure under Serializable isolation level).</summary>
    public const string SerializationFailureSqlState = "40001";

    /// <summary>Specifies PostgreSQL SQLSTATE 40P01 (deadlock detected between concurrent transactions).</summary>
    public const string DeadlockDetectedSqlState = "40P01";

    /// <summary>Specifies PostgreSQL SQLSTATE 25P02 (current transaction is aborted, commands ignored until end of transaction block).</summary>
    public const string InFailedSqlTransactionSqlState = "25P02";

    /// <summary>Specifies PostgreSQL SQLSTATE 57014 (query was canceled by client or statement timeout).</summary>
    public const string QueryCanceledSqlState = "57014";

    /// <summary>Specifies PostgreSQL SQLSTATE 57P01 (server administrator shutdown).</summary>
    public const string AdminShutdownSqlState = "57P01";

    /// <summary>Specifies PostgreSQL SQLSTATE 57P02 (server crash shutdown).</summary>
    public const string CrashShutdownSqlState = "57P02";

    /// <summary>Specifies PostgreSQL SQLSTATE 57P03 (cannot connect now, server starting up).</summary>
    public const string CannotConnectNowSqlState = "57P03";

    /// <summary>Specifies PostgreSQL SQLSTATE 08006 (connection failure).</summary>
    public const string ConnectionFailureSqlState = "08006";

    /// <summary>
    /// Determines whether the specified exception represents a PostgreSQL serialization conflict (SQLSTATE 40001).
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a serialization conflict; otherwise, <see langword="false"/>.</returns>
    public static bool IsSerializationFailure(Exception? exception)
    {
        if (exception is PostgresException pgEx && pgEx.SqlState == SerializationFailureSqlState)
        {
            return true;
        }

        return exception?.InnerException is not null && IsSerializationFailure(exception.InnerException);
    }

    /// <summary>
    /// Determines whether the specified exception represents a PostgreSQL deadlock (SQLSTATE 40P01).
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a deadlock; otherwise, <see langword="false"/>.</returns>
    public static bool IsDeadlock(Exception? exception)
    {
        if (exception is PostgresException pgEx && pgEx.SqlState == DeadlockDetectedSqlState)
        {
            return true;
        }

        return exception?.InnerException is not null && IsDeadlock(exception.InnerException);
    }

    /// <summary>
    /// Determines whether the specified exception indicates that the transaction has entered an aborted state (SQLSTATE 25P02).
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the transaction is in an aborted state; otherwise, <see langword="false"/>.</returns>
    public static bool IsInFailedTransaction(Exception? exception)
    {
        if (exception is PostgresException pgEx && pgEx.SqlState == InFailedSqlTransactionSqlState)
        {
            return true;
        }

        return exception?.InnerException is not null && IsInFailedTransaction(exception.InnerException);
    }

    /// <summary>
    /// Determines whether the specified exception represents a transient failure suitable for retry.
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a transient failure; otherwise, <see langword="false"/>.</returns>
    public static bool IsTransient(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (IsSerializationFailure(exception) || IsDeadlock(exception))
        {
            return true;
        }

        if (exception is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                ConnectionFailureSqlState => true,
                AdminShutdownSqlState => true,
                CrashShutdownSqlState => true,
                CannotConnectNowSqlState => true,
                _ => false
            };
        }

        if (exception is NpgsqlException npgEx && npgEx.IsTransient)
        {
            return true;
        }

        if (exception is TimeoutException)
        {
            return true;
        }

        return exception.InnerException is not null && IsTransient(exception.InnerException);
    }
}
