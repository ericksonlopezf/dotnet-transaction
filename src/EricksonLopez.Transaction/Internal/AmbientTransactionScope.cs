// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Encapsulates an underlying <see cref="ITransaction"/> to manage ambient <see cref="AsyncLocal{T}"/> context lifetime.
/// </summary>
internal sealed class AmbientTransactionScope : ITransaction
{
    private readonly ITransaction _innerTransaction;
    private readonly ITransactionContext? _previousContext;
    private readonly AsyncLocal<ITransactionContext?> _ambientContextHolder;
    private readonly Action? _onDisposed;
    private int _disposed;

    public AmbientTransactionScope(
        ITransaction innerTransaction,
        ITransactionContext? previousContext,
        AsyncLocal<ITransactionContext?> ambientContextHolder,
        Action? onDisposed = null)
    {
        _innerTransaction = innerTransaction ?? throw new ArgumentNullException(nameof(innerTransaction));
        _previousContext = previousContext;
        _ambientContextHolder = ambientContextHolder ?? throw new ArgumentNullException(nameof(ambientContextHolder));
        _onDisposed = onDisposed;

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
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposed, 0, 0) == 1, this);
        return _innerTransaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposed, 0, 0) == 1, this);
        return _innerTransaction.RollbackAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposed, 0, 0) == 1, this);
        return _innerTransaction.CreateSavepointAsync(name, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        _ambientContextHolder.Value = _previousContext;
        _onDisposed?.Invoke();
        return _innerTransaction.DisposeAsync();
    }
}
