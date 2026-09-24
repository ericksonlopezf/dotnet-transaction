// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Represents a nested transaction scope adapter that executes non-transactionally, suppressing any ambient transaction context.
/// </summary>
internal sealed class SuppressedTransactionScope : ITransaction
{
    private readonly ITransactionContext? _previousContext;
    private readonly AsyncLocal<ITransactionContext?> _ambientContextHolder;
    private readonly TransactionStateMachine _stateMachine;
    private readonly Action? _onDisposed;
    private readonly bool _previousSuppressed;
    private int _disposed;

    public SuppressedTransactionScope(
        ITransactionContext? previousContext,
        AsyncLocal<ITransactionContext?> ambientContextHolder,
        Action? onDisposed = null)
    {
        _previousContext = previousContext;
        _ambientContextHolder = ambientContextHolder ?? throw new ArgumentNullException(nameof(ambientContextHolder));
        _onDisposed = onDisposed;
        _stateMachine = new TransactionStateMachine(TransactionState.Active);
        TransactionId = Guid.NewGuid();

        // Suppress the ambient context and mark suppression active
        _previousSuppressed = TransactionManager.IsSuppressedHolder.Value;
        TransactionManager.IsSuppressedHolder.Value = true;
        _ambientContextHolder.Value = null;
    }

    /// <inheritdoc/>
    public Guid TransactionId { get; }

    /// <inheritdoc/>
    public ITransactionContext Context => throw new InvalidOperationException("A suppressed transaction scope does not provide an active ITransactionContext.");

    /// <inheritdoc/>
    public TransactionState State => _stateMachine.CurrentState;

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        _stateMachine.TransitionToCommitted();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        _stateMachine.TransitionToRolledBack();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        throw new InvalidOperationException("Cannot create savepoints on a suppressed transaction scope.");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        TransactionManager.IsSuppressedHolder.Value = _previousSuppressed;
        _ambientContextHolder.Value = _previousContext;
        _onDisposed?.Invoke();

        if (_stateMachine.CurrentState == TransactionState.Active)
        {
            _stateMachine.TransitionToDisposed();
        }

        return ValueTask.CompletedTask;
    }
}
