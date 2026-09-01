// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction.Mediator;

/// <summary>
/// Specifies transactional requirements, isolation level, and timeout for a mediator command.
/// </summary>
/// <remarks>
/// This attribute is no longer inspected by <see cref="TransactionPipelineBehavior{TRequest, TResponse}"/>
/// as of the AOT-safety refactoring. Implement <see cref="ITransactionalCommandOptions"/> on your command
/// to specify custom transaction options without reflection.
/// </remarks>
[Obsolete("Use ITransactionalCommandOptions interface instead. This attribute requires reflection and is incompatible with Native AOT trimming.", error: false)]
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
