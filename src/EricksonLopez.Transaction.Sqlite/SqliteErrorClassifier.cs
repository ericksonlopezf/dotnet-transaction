// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Data.Sqlite;

namespace EricksonLopez.Transaction.Sqlite;

/// <summary>
/// Provides classification methods for SQLite error codes, database busy conditions, and lock conflicts.
/// </summary>
public static class SqliteErrorClassifier
{
    private const int SqliteBusy = 5;       // SQLITE_BUSY: The database file is locked
    private const int SqliteLocked = 6;     // SQLITE_LOCKED: A table in the database is locked
    private const int SqliteConstraint = 19;// SQLITE_CONSTRAINT: Abort due to constraint violation

    /// <summary>
    /// Determines whether the specified exception represents a SQLite busy or locked condition.
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a busy or locked state; otherwise, <see langword="false"/>.</returns>
    public static bool IsBusyOrLocked(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is SqliteException sqliteEx &&
            (sqliteEx.SqliteErrorCode == SqliteBusy || sqliteEx.SqliteErrorCode == SqliteLocked))
        {
            return true;
        }

        return exception.InnerException is not null && IsBusyOrLocked(exception.InnerException);
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

        if (IsBusyOrLocked(exception))
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
