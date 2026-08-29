// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction.Exceptions;

/// <summary>
/// Represents an exception thrown when a rollback operation fails explicitly during transaction teardown.
/// </summary>
public sealed class TransactionRollbackException : TransactionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionRollbackException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TransactionRollbackException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionRollbackException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying provider exception during rollback.</param>
    public TransactionRollbackException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
