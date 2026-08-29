// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction;

/// <summary>
/// Represents an explicit handle to an active database transaction lifecycle.
/// </summary>
/// <remarks>
/// Consumers should always manage instances of this interface within an <see langword="await using"/> block.
/// If <see cref="CommitAsync"/> is not called before disposal, the transaction is automatically rolled back.
/// </remarks>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier of the transaction.
    /// </summary>
    Guid TransactionId { get; }

    /// <summary>
    /// Gets the execution context associated with this transaction.
    /// </summary>
    ITransactionContext Context { get; }

    /// <summary>
    /// Gets the current state of the transaction.
    /// </summary>
    TransactionState State { get; }

    /// <summary>
    /// Commits the active transaction and persists all changes atomically to storage.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the active transaction and discards all uncommitted modifications.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous rollback operation.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a named savepoint within this transaction.
    /// </summary>
    /// <param name="name">The unique name that identifies the savepoint within the transaction.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created <see cref="ISavepoint"/>.</returns>
    Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default);
}
