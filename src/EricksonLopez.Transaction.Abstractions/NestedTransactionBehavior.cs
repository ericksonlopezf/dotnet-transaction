// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Transaction;

/// <summary>
/// Specifies how a transaction coordinator handles nested execution scopes when an ambient transaction is already active.
/// </summary>
public enum NestedTransactionBehavior
{
    /// <summary>
    /// Creates a named savepoint when an active transaction exists, rolling back only the nested scope on failure.
    /// </summary>
    UseSavepoint = 0,

    /// <summary>
    /// Requires an independent physical transaction on a separate database connection, suspending any ambient transaction.
    /// </summary>
    RequireNew = 1,

    /// <summary>
    /// Executes the nested scope non-transactionally without ambient transaction enlistment.
    /// </summary>
    Suppress = 2,

    /// <summary>
    /// Joins the existing active transaction without creating savepoints, causing any failure to invalidate the entire transaction.
    /// </summary>
    JoinExisting = 3
}
