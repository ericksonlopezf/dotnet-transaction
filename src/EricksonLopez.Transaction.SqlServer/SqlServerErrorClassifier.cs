// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Data.SqlClient;

namespace EricksonLopez.Transaction.SqlServer;

/// <summary>
/// Provides classification methods for Microsoft SQL Server error codes, deadlocks, and serialization conflicts.
/// </summary>
public static class SqlServerErrorClassifier
{
    // SQL Server Error Numbers
    private const int DeadlockErrorNumber = 1205;
    private const int SnapshotConflictErrorNumber = 3960;
    private const int UpdateConflictErrorNumber = 3961;
    private const int TimeoutExpired = -2;

    /// <summary>
    /// Determines whether the specified exception represents a SQL Server deadlock (Error 1205).
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a deadlock; otherwise, <see langword="false"/>.</returns>
    public static bool IsDeadlock(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is SqlException sqlEx && sqlEx.Number == DeadlockErrorNumber)
        {
            return true;
        }

        return exception.InnerException is not null && IsDeadlock(exception.InnerException);
    }

    /// <summary>
    /// Determines whether the specified exception represents a SQL Server snapshot isolation update conflict (Error 3960 or 3961).
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a serialization conflict; otherwise, <see langword="false"/>.</returns>
    public static bool IsSerializationFailure(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is SqlException sqlEx &&
            (sqlEx.Number == SnapshotConflictErrorNumber || sqlEx.Number == UpdateConflictErrorNumber))
        {
            return true;
        }

        return exception.InnerException is not null && IsSerializationFailure(exception.InnerException);
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

        if (IsDeadlock(exception) || IsSerializationFailure(exception))
        {
            return true;
        }

        if (exception is SqlException sqlEx)
        {
            return sqlEx.Number switch
            {
                TimeoutExpired => true,
                4060 => true,   // Cannot open database requested by the login
                40197 => true,  // Error processing the request
                40501 => true,  // Server is busy
                40613 => true,  // Database is not currently available
                49918 => true,  // Cannot process request. Not enough resources
                49919 => true,  // Cannot process create or update request. Too many operations
                49920 => true,  // Cannot process request. Too many operations
                _ => false
            };
        }

        if (exception is TimeoutException)
        {
            return true;
        }

        return exception.InnerException is not null && IsTransient(exception.InnerException);
    }
}
