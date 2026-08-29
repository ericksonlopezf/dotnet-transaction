// Copyright © Erickson Lopez. MIT License.
using System;
using Oracle.ManagedDataAccess.Client;

namespace EricksonLopez.Transaction.Oracle;

/// <summary>
/// Provides classification methods for Oracle Database error codes, deadlocks, serialization conflicts, and transient failures.
/// </summary>
public static class OracleErrorClassifier
{
    private const int DeadlockErrorNumber = 60;              // ORA-00060: deadlock detected while waiting for resource
    private const int SerializationFailureErrorNumber = 8177; // ORA-08177: can't serialize access for this transaction
    private const int UniqueConstraintViolation = 1;         // ORA-00001: unique constraint violated

    /// <summary>
    /// Determines whether the specified exception represents an Oracle deadlock (ORA-00060).
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a deadlock; otherwise, <see langword="false"/>.</returns>
    public static bool IsDeadlock(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is OracleException oracleEx && oracleEx.Number == DeadlockErrorNumber)
        {
            return true;
        }

        return exception.InnerException is not null && IsDeadlock(exception.InnerException);
    }

    /// <summary>
    /// Determines whether the specified exception represents an Oracle serialization conflict (ORA-08177).
    /// </summary>
    /// <param name="exception">The exception to inspect, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the exception represents a serialization failure; otherwise, <see langword="false"/>.</returns>
    public static bool IsSerializationFailure(Exception? exception)
    {
        if (exception is null)
        {
            return false;
        }

        if (exception is OracleException oracleEx && oracleEx.Number == SerializationFailureErrorNumber)
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

        if (exception is OracleException oracleEx)
        {
            return oracleEx.Number switch
            {
                12541 => true, // TNS:no listener
                12543 => true, // TNS:destination host unreachable
                12170 => true, // TNS:Connect timeout occurred
                12571 => true, // TNS:packet writer failure
                3113 => true,  // end-of-file on communication channel
                3114 => true,  // not connected to ORACLE
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
