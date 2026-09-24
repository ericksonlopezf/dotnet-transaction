// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction;

/// <summary>
/// Provides access to the active database connection, transaction primitive, state, and savepoints during transactional execution.
/// </summary>
/// <remarks>
/// Repositories and persistence adapters receive this context to execute atomic SQL operations
/// without needing to manage transaction lifecycle directly.
/// </remarks>
public interface ITransactionContext : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier of this transaction execution context.
    /// </summary>
    Guid TransactionId { get; }

    /// <summary>
    /// Gets the underlying active database connection associated with this transaction.
    /// </summary>
    /// <remarks>
    /// Do not manually invoke <c>Dispose()</c>, <see cref="System.Data.Common.DbConnection.Close()"/>, 
    /// or <see cref="System.Data.Common.DbConnection.BeginTransaction()"/> on this instance. 
    /// Mutating the connection state manually circumvents the state machine and corrupts the transaction boundary.
    /// </remarks>
    DbConnection Connection { get; }

    /// <summary>
    /// Gets the underlying active database transaction.
    /// </summary>
    DbTransaction Transaction { get; }

    /// <summary>
    /// Gets the current lifecycle state of the transaction.
    /// </summary>
    TransactionState State { get; }

    /// <summary>
    /// Gets the isolation level configured for this transaction.
    /// </summary>
    TransactionIsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Gets the cancellation token scoped to this transaction execution.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the read-only collection of enlistments attached to this transaction lifecycle.
    /// </summary>
    IReadOnlyList<ITransactionEnlistment> Enlistments { get; }

    /// <summary>
    /// Creates a named savepoint within this transaction for partial rollback.
    /// </summary>
    /// <param name="name">The unique name for the savepoint.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the created <see cref="ISavepoint"/>.</returns>
    Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enlists a participant in the lifecycle notifications of this transaction.
    /// </summary>
    /// <param name="enlistment">The enlistment participant to register.</param>
    void Enlist(ITransactionEnlistment enlistment);

    /// <summary>
    /// Gets a value indicating whether this transaction context has been marked rollback-only.
    /// </summary>
    bool IsRollbackOnly { get; }

    /// <summary>
    /// Marks this transaction context as rollback-only, preventing subsequent commits.
    /// </summary>
    /// <param name="reason">The diagnostic reason for marking the transaction rollback-only.</param>
    void SetRollbackOnly(string reason);
}
