// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Wraps an underlying <see cref="ITransaction"/> to manage ambient <see cref="AsyncLocal{T}"/> context lifetime.
/// </summary>
internal sealed class AmbientTransactionScope : ITransaction
{
    private readonly ITransaction _innerTransaction;
    private readonly ITransactionContext? _previousContext;
    private readonly AsyncLocal<ITransactionContext?> _ambientContextHolder;
    private bool _disposed;

    public AmbientTransactionScope(
        ITransaction innerTransaction,
        ITransactionContext? previousContext,
        AsyncLocal<ITransactionContext?> ambientContextHolder)
    {
        _innerTransaction = innerTransaction ?? throw new ArgumentNullException(nameof(innerTransaction));
        _previousContext = previousContext;
        _ambientContextHolder = ambientContextHolder ?? throw new ArgumentNullException(nameof(ambientContextHolder));

        // Set the active transaction context as ambient
        _ambientContextHolder.Value = _innerTransaction.Context;
    }

    /// <inheritdoc/>
    public Guid TransactionId => _innerTransaction.TransactionId;

    /// <inheritdoc/>
    public ITransactionContext Context => _innerTransaction.Context;

    /// <inheritdoc/>
    public TransactionState State => _innerTransaction.State;

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _innerTransaction.CommitAsync(cancellationToken);

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
        => _innerTransaction.RollbackAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
        => _innerTransaction.CreateSavepointAsync(name, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await _innerTransaction.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            // Restore previous ambient context
            _ambientContextHolder.Value = _previousContext;
        }
    }
}
