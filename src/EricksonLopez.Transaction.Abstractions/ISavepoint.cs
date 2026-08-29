// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction;

/// <summary>
/// Represents a named savepoint within an active transaction, enabling partial rollback without aborting the outer transaction.
/// </summary>
public interface ISavepoint : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier or name of the savepoint.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Rolls back all operations executed within the transaction since this savepoint was created.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous rollback operation.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the savepoint in engines that support savepoint destruction.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous release operation.</returns>
    Task ReleaseAsync(CancellationToken cancellationToken = default);
}
