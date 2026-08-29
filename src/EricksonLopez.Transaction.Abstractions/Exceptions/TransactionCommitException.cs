// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction.Exceptions;

/// <summary>
/// Represents an exception thrown when a transaction commit operation fails or when the commit outcome is ambiguous.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architectural Warning (Commit Ambiguity):</strong>
/// An exception during commit does not guarantee that the transaction was rolled back.
/// In scenarios involving network drops, TCP connection timeouts, or database server restarts,
/// the database storage engine may have committed the transaction to disk before the acknowledgment
/// packet reached the client application.
/// </para>
/// <para>
/// Applications should rely on distributed idempotency keys and outbox reconciliation
/// rather than assuming uncommitted state upon receiving this error.
/// </para>
/// </remarks>
public sealed class TransactionCommitException : TransactionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCommitException"/> class.
    /// </summary>
    /// <param name="message">The error message explaining the commit failure.</param>
    /// <param name="isAmbiguous">A value indicating whether the commit outcome is uncertain due to network disconnection or timeout.</param>
    public TransactionCommitException(string message, bool isAmbiguous = false)
        : base(message)
    {
        IsAmbiguous = isAmbiguous;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCommitException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception thrown by the database provider.</param>
    /// <param name="isAmbiguous">A value indicating whether the commit outcome is uncertain.</param>
    public TransactionCommitException(string message, Exception innerException, bool isAmbiguous = false)
        : base(message, innerException)
    {
        IsAmbiguous = isAmbiguous;
    }

    /// <summary>
    /// Gets a value indicating whether the final commit status of the transaction is indeterminate.
    /// </summary>
    public bool IsAmbiguous { get; }
}
