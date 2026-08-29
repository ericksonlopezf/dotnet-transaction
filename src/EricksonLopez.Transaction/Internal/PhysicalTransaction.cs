// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.Diagnostics;
using EricksonLopez.Transaction.Exceptions;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Physical implementation of <see cref="ITransaction"/> managing underlying ADO.NET connection and transaction lifecycles.
/// </summary>
internal sealed class PhysicalTransaction : ITransaction
{
    private readonly TransactionContext _context;
    private readonly TransactionStateMachine _stateMachine;
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly bool _ownsConnection;
    private readonly long _startTimestamp;
    private readonly Activity? _activity;
    private bool _disposed;

    public PhysicalTransaction(
        TransactionContext context,
        TransactionStateMachine stateMachine,
        DbConnection connection,
        DbTransaction transaction,
        bool ownsConnection,
        string? transactionName = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _ownsConnection = ownsConnection;
        _startTimestamp = Stopwatch.GetTimestamp();

        _activity = TransactionDiagnostics.StartActivity(
            "Transaction.Execute",
            _context.TransactionId,
            _context.IsolationLevel,
            transactionName);

        TransactionDiagnostics.RecordStarted(_context.IsolationLevel);
    }

    /// <inheritdoc/>
    public Guid TransactionId => _context.TransactionId;

    /// <inheritdoc/>
    public ITransactionContext Context => _context;

    /// <inheritdoc/>
    public TransactionState State => _stateMachine.CurrentState;

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_context.CancellationToken, cancellationToken);
        CancellationToken combinedToken = linkedCts.Token;

        try
        {
            await _context.ExecuteBeforeCommitHooksAsync(combinedToken).ConfigureAwait(false);

            await _transaction.CommitAsync(combinedToken).ConfigureAwait(false);

            _stateMachine.TransitionToCommitted();

            double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
            TransactionDiagnostics.RecordCommitted(_context.IsolationLevel, elapsedMs);
            _activity?.SetTag("transaction.outcome", "committed");
            _activity?.SetStatus(ActivityStatusCode.Ok);

            await _context.ExecuteAfterCommitHooksAsync(combinedToken).ConfigureAwait(false);
        }
        catch (TransactionStateException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _stateMachine.TransitionToFailed();
            throw;
        }
        catch (Exception ex)
        {
            _stateMachine.TransitionToFailed();

            double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
            TransactionDiagnostics.RecordFailed(_context.IsolationLevel, elapsedMs, ex.GetType().Name);
            _activity?.SetTag("transaction.outcome", "failed");
            _activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            await _context.ExecuteOnExceptionHooksAsync(ex, CancellationToken.None).ConfigureAwait(false);

            // An exception during Commit is inherently ambiguous (network could have dropped after DB committed)
            throw new TransactionCommitException(
                $"Failed to commit transaction '{TransactionId}'. The final database state may be indeterminate.",
                ex,
                isAmbiguous: true);
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_context.CancellationToken, cancellationToken);
        CancellationToken combinedToken = linkedCts.Token;

        try
        {
            await _transaction.RollbackAsync(combinedToken).ConfigureAwait(false);
            _stateMachine.TransitionToRolledBack();

            double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
            TransactionDiagnostics.RecordRolledBack(_context.IsolationLevel, elapsedMs);
            _activity?.SetTag("transaction.outcome", "rolled_back");

            await _context.ExecuteAfterRollbackHooksAsync(combinedToken).ConfigureAwait(false);
        }
        catch (TransactionStateException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _stateMachine.TransitionToFailed();
            throw;
        }
        catch (Exception ex)
        {
            _stateMachine.TransitionToFailed();
            await _context.ExecuteOnExceptionHooksAsync(ex, CancellationToken.None).ConfigureAwait(false);
            throw new TransactionRollbackException($"Failed to rollback transaction '{TransactionId}'.", ex);
        }
    }

    /// <inheritdoc/>
    public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _context.CreateSavepointAsync(name, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activity?.Dispose();

        TransactionState currentState = _stateMachine.CurrentState;
        if (currentState is TransactionState.Active or TransactionState.Failed or TransactionState.Created)
        {
            try
            {
                await _transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                _stateMachine.TransitionToRolledBack();
                await _context.ExecuteAfterRollbackHooksAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Silently swallow rollback errors during disposal as connection may already be broken or closed
            }
        }

        try
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore transaction disposal failures
        }

        if (_ownsConnection)
        {
            try
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore connection disposal failures
            }
        }

        _stateMachine.TransitionToDisposed();
    }
}
