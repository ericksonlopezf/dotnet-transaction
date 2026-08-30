// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction.Mediator;

/// <summary>
/// Specifies transactional requirements, isolation level, and timeout for a mediator command.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
public sealed class TransactionalAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the transaction isolation level.
    /// </summary>
    public TransactionIsolationLevel IsolationLevel { get; set; } = TransactionIsolationLevel.ReadCommitted;

    /// <summary>
    /// Gets or sets the transaction timeout in seconds. A value of 0 indicates the system default.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionalAttribute"/> class.
    /// </summary>
    public TransactionalAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionalAttribute"/> class with a specific isolation level.
    /// </summary>
    /// <param name="isolationLevel">The transaction isolation level.</param>
    public TransactionalAttribute(TransactionIsolationLevel isolationLevel)
    {
        IsolationLevel = isolationLevel;
    }
}
