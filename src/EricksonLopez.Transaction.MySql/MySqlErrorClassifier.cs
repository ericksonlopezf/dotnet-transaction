// Copyright © Erickson Lopez. MIT License.
using System;
using MySqlConnector;

namespace EricksonLopez.Transaction.MySql;

/// <summary>
/// Provides classification methods for MySQL error codes, deadlocks, lock wait timeouts, and transient connection failures.
/// </summary>
public static class MySqlErrorClassifier
{
    private const int DeadlockErrorNumber = 1213;             // ER_LOCK_DEADLOCK
    private const int LockWaitTimeoutErrorNumber = 1205;       // ER_LOCK_WAIT_TIMEOUT
    private const int ServerShutdown = 1053;                  // ER_SERVER_SHUTDOWN
    private const int UnableToConnectToHost = 2003;           // CR_CONN_HOST_ERROR
    private const int ConnectionLost = 2013;                  // CR_SERVER_LOST

    /// <summary>
    /// Determines whether the specified exception represents a MySQL deadlock.
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a deadlock; otherwise, <see langword="false"/>.</returns>
    public static bool IsDeadlock(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is MySqlException mySqlEx && mySqlEx.Number == DeadlockErrorNumber)
        {
            return true;
        }

        return exception.InnerException is not null && IsDeadlock(exception.InnerException);
    }

    /// <summary>
    /// Determines whether the specified exception represents a MySQL lock wait timeout.
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a lock wait timeout; otherwise, <see langword="false"/>.</returns>
    public static bool IsLockWaitTimeout(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is MySqlException mySqlEx && mySqlEx.Number == LockWaitTimeoutErrorNumber)
        {
            return true;
        }

        return exception.InnerException is not null && IsLockWaitTimeout(exception.InnerException);
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

        if (IsDeadlock(exception) || IsLockWaitTimeout(exception))
        {
            return true;
        }

        if (exception is MySqlException mySqlEx)
        {
            return mySqlEx.Number switch
            {
                ServerShutdown => true,
                UnableToConnectToHost => true,
                ConnectionLost => true,
                _ => mySqlEx.IsTransient
            };
        }

        if (exception is TimeoutException)
        {
            return true;
        }

        return exception.InnerException is not null && IsTransient(exception.InnerException);
    }
}
