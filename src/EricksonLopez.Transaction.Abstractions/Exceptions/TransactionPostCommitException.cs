// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction.Exceptions;

/// <summary>
/// Represents an exception thrown when the database transaction was committed successfully,
/// but one or more post-commit hooks failed during execution.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architectural Invariant:</strong>
/// When this exception is thrown, the underlying physical database transaction has already been committed
/// durably to disk. Application compensation logic must not attempt to rollback the database transaction,
/// as the database state is irrevocably committed.
/// </para>
/// </remarks>
public sealed class TransactionPostCommitException : TransactionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPostCommitException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TransactionPostCommitException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPostCommitException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception thrown by post-commit hook execution.</param>
    public TransactionPostCommitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
