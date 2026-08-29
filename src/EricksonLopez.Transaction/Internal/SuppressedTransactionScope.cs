// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Nested transaction scope adapter that executes non-transactionally, suppressing any ambient transaction context.
/// </summary>
internal sealed class SuppressedTransactionScope : ITransaction
{
    private readonly ITransactionContext? _previousContext;
    private readonly AsyncLocal<ITransactionContext?> _ambientContextHolder;
    private readonly TransactionStateMachine _stateMachine;
    private bool _disposed;

    public SuppressedTransactionScope(
        ITransactionContext? previousContext,
        AsyncLocal<ITransactionContext?> ambientContextHolder)
    {
        _previousContext = previousContext;
        _ambientContextHolder = ambientContextHolder ?? throw new ArgumentNullException(nameof(ambientContextHolder));
        _stateMachine = new TransactionStateMachine(TransactionState.Active);
        TransactionId = Guid.NewGuid();

        // Suppress the ambient context
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        _stateMachine.TransitionToCommitted();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _stateMachine.TransitionToRolledBack();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        throw new InvalidOperationException("Cannot create savepoints on a suppressed transaction scope.");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _ambientContextHolder.Value = _previousContext;

        if (_stateMachine.CurrentState == TransactionState.Active)
        {
            _stateMachine.TransitionToDisposed();
        }

        return ValueTask.CompletedTask;
    }
}
