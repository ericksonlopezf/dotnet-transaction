// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction.Mediator;

/// <summary>
/// Marks a mediator command with transactional intent, declaring the intended isolation level and timeout
/// for documentation, tooling, and architectural visibility purposes.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is a <strong>metadata marker</strong>. It communicates intent to developers and tooling,
/// but it does <strong>not</strong> have a runtime effect on the transaction pipeline by itself.
/// <see cref="TransactionPipelineBehavior{TRequest, TResponse}"/> does not read or process this attribute
/// during execution.
/// </para>
/// <para>
/// To configure transaction options that are <strong>functionally applied</strong> by the pipeline at runtime,
/// implement <see cref="ITransactionalCommandOptions"/> on the command class. This interface-based approach
/// is also fully compatible with Native AOT and trimming since it requires no reflection.
/// </para>
/// <para>
/// You may use <see cref="TransactionalAttribute"/> alongside <see cref="ITransactionalCommandOptions"/> for
/// documentation clarity — the attribute communicates intent while the interface enforces runtime behavior.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
public sealed class TransactionalAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the transaction isolation level.
    /// </summary>
    public TransactionIsolationLevel IsolationLevel { get; set; } = TransactionIsolationLevel.ReadCommitted;

    /// <summary>
    /// Gets or sets the transaction timeout in seconds, where zero indicates the system default timeout.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionalAttribute"/> class.
    /// </summary>
    public TransactionalAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionalAttribute"/> class with the specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The transaction isolation level.</param>
    public TransactionalAttribute(TransactionIsolationLevel isolationLevel)
    {
        IsolationLevel = isolationLevel;
    }
}
