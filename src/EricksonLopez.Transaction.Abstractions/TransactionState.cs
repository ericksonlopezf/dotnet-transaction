// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Transaction;

/// <summary>
/// Specifies the lifecycle state of a transaction.
/// </summary>
public enum TransactionState
{
    /// <summary>
    /// Specifies that the transaction instance has been created but the physical transaction has not yet begun.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Specifies that the transaction is actively executing and accepting operations.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Specifies that the transaction has successfully committed all modifications to storage.
    /// </summary>
    Committed = 2,

    /// <summary>
    /// Specifies that the transaction has rolled back and all modified state was discarded.
    /// </summary>
    RolledBack = 3,

    /// <summary>
    /// Specifies that the transaction encountered an unhandled error or ambiguous failure.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Specifies that the transaction has completed its lifecycle and released all resources.
    /// </summary>
    Disposed = 5
}
