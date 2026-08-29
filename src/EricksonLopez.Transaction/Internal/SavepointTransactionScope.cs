// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.Exceptions;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Nested transaction scope adapter backed by a database savepoint on an active physical transaction.
/// </summary>
internal sealed class SavepointTransactionScope : ITransaction
{
    private readonly ITransactionContext _parentContext;
    private readonly ISavepoint _savepoint;
    private readonly TransactionStateMachine _stateMachine;
    private bool _disposed;

    public SavepointTransactionScope(ITransactionContext parentContext, ISavepoint savepoint)
    {
        _parentContext = parentContext ?? throw new ArgumentNullException(nameof(parentContext));
        _savepoint = savepoint ?? throw new ArgumentNullException(nameof(savepoint));
        _stateMachine = new TransactionStateMachine(TransactionState.Active);
        TransactionId = Guid.NewGuid();
    }

    /// <inheritdoc/>
    public Guid TransactionId { get; }

    /// <inheritdoc/>
    public ITransactionContext Context => _parentContext;

    /// <inheritdoc/>
    public TransactionState State => _stateMachine.CurrentState;

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _savepoint.ReleaseAsync(cancellationToken).ConfigureAwait(false);
            _stateMachine.TransitionToCommitted();
        }
        catch (Exception ex)
        {
            _stateMachine.TransitionToFailed();
            throw new TransactionCommitException($"Failed to release savepoint '{_savepoint.Name}' during commit.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _savepoint.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _stateMachine.TransitionToRolledBack();
        }
        catch (Exception ex)
        {
            _stateMachine.TransitionToFailed();
            throw new TransactionRollbackException($"Failed to rollback to savepoint '{_savepoint.Name}'.", ex);
        }
    }

    /// <inheritdoc/>
    public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _parentContext.CreateSavepointAsync(name, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_stateMachine.CurrentState is TransactionState.Active or TransactionState.Failed)
        {
            try
            {
                await _savepoint.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Silently swallow rollback errors during disposal
            }
        }

        await _savepoint.DisposeAsync().ConfigureAwait(false);
        _stateMachine.TransitionToDisposed();
    }
}
