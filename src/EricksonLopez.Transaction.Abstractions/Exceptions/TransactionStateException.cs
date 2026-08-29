// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction.Exceptions;

/// <summary>
/// Represents an exception thrown when an operation is attempted on a transaction in an incompatible state.
/// </summary>
public sealed class TransactionStateException : TransactionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionStateException"/> class.
    /// </summary>
    /// <param name="actualState">The current state of the transaction.</param>
    /// <param name="attemptedOperation">The operation that was invalid for the current state.</param>
    public TransactionStateException(TransactionState actualState, string attemptedOperation)
        : base($"Cannot execute '{attemptedOperation}' because the transaction is in the '{actualState}' state.")
    {
        ActualState = actualState;
        AttemptedOperation = attemptedOperation;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionStateException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="message">The descriptive error message.</param>
    /// <param name="innerException">The inner exception cause.</param>
    public TransactionStateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets the actual state of the transaction when the violation occurred.
    /// </summary>
    public TransactionState ActualState { get; }

    /// <summary>
    /// Gets the name of the operation that was attempted.
    /// </summary>
    public string? AttemptedOperation { get; }
}
