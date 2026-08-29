// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction.Exceptions;

/// <summary>
/// Represents an exception thrown when a transaction execution duration exceeds its configured timeout threshold.
/// </summary>
public sealed class TransactionTimeoutException : TransactionException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionTimeoutException"/> class with the specified timeout.
    /// </summary>
    /// <param name="timeout">The configured timeout duration that elapsed.</param>
    public TransactionTimeoutException(TimeSpan timeout)
        : base($"The transaction exceeded its configured timeout of {timeout.TotalMilliseconds}ms and was aborted.")
    {
        Timeout = timeout;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionTimeoutException"/> class with a custom message, inner exception, and timeout.
    /// </summary>
    /// <param name="message">The descriptive error message.</param>
    /// <param name="innerException">The inner exception cause.</param>
    /// <param name="timeout">The configured timeout duration.</param>
    public TransactionTimeoutException(string message, Exception innerException, TimeSpan timeout)
        : base(message, innerException)
    {
        Timeout = timeout;
    }

    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }
}
