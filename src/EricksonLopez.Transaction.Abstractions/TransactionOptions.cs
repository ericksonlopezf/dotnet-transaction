// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction;

/// <summary>
/// Represents immutable configuration options for controlling transaction behavior, isolation level, timeout, and nesting semantics.
/// </summary>
public sealed record TransactionOptions
{
    /// <summary>
    /// Gets the default transaction options with <see cref="TransactionIsolationLevel.ReadCommitted"/> and <see cref="NestedTransactionBehavior.UseSavepoint"/>.
    /// </summary>
    public static readonly TransactionOptions Default = new();

    /// <summary>
    /// Gets the requested isolation level for the transaction.
    /// </summary>
    public TransactionIsolationLevel IsolationLevel { get; init; } = TransactionIsolationLevel.ReadCommitted;

    /// <summary>
    /// Gets the maximum duration allowed for the transaction before timing out, or <see langword="null"/> for default driver timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction should be opened in read-only mode where supported by the provider.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Gets the behavior applied when an execution scope is nested inside an existing transaction.
    /// </summary>
    public NestedTransactionBehavior NestedBehavior { get; init; } = NestedTransactionBehavior.UseSavepoint;

    /// <summary>
    /// Gets the optional logical name or identifier for the transaction, used in diagnostics and logging.
    /// </summary>
    public string? TransactionName { get; init; }

    /// <summary>
    /// Gets a new <see cref="TransactionOptions"/> instance configured with <see cref="TransactionIsolationLevel.Serializable"/>.
    /// </summary>
    public static TransactionOptions Serializable => new() { IsolationLevel = TransactionIsolationLevel.Serializable };

    /// <summary>
    /// Gets a new <see cref="TransactionOptions"/> instance configured for read-only execution.
    /// </summary>
    public static TransactionOptions ReadOnlyMode => new() { ReadOnly = true };

    /// <summary>
    /// Creates a new <see cref="TransactionOptions"/> instance with the specified timeout.
    /// </summary>
    /// <param name="timeout">The transaction execution timeout.</param>
    /// <returns>A new <see cref="TransactionOptions"/> instance configured with the specified timeout.</returns>
    public static TransactionOptions WithTimeout(TimeSpan timeout) => new() { Timeout = timeout };
}
