// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Nested transaction scope adapter that participates in an existing active transaction without creating savepoints.
/// </summary>
internal sealed class JoinExistingTransactionScope : ITransaction
{
    private readonly ITransactionContext _parentContext;
    private readonly TransactionStateMachine _stateMachine;
    private bool _disposed;

    public JoinExistingTransactionScope(ITransactionContext parentContext)
    {
        _parentContext = parentContext ?? throw new ArgumentNullException(nameof(parentContext));
        _stateMachine = new TransactionStateMachine(TransactionState.Active);
        TransactionId = parentContext.TransactionId;
    }

    /// <inheritdoc/>
    public Guid TransactionId { get; }

    /// <inheritdoc/>
    public ITransactionContext Context => _parentContext;

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
        return _parentContext.CreateSavepointAsync(name, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _stateMachine.TransitionToDisposed();
        return ValueTask.CompletedTask;
    }
}
