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
/// Represents the physical implementation of <see cref="ITransaction"/> managing underlying ADO.NET connection and transaction lifecycles.
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
    private readonly SemaphoreSlim _gate = new(1, 1);
    private const string OutcomeTag = "transaction.outcome";
    private int _disposed;

    public PhysicalTransaction(
        TransactionContext context,
        TransactionStateMachine stateMachine,
        DbConnection connection,
        DbTransaction transaction,
        bool ownsConnection,
        string? transactionName = null,
        bool sanitizeTelemetryMetadata = false)
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
            transactionName,
            sanitizeTelemetryMetadata);

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
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_context.CancellationToken, cancellationToken);
        CancellationToken combinedToken = linkedCts.Token;

        if (combinedToken.IsCancellationRequested)
        {
            _stateMachine.TransitionToFailed();
            combinedToken.ThrowIfCancellationRequested();
        }

        try
        {
            await _gate.WaitAsync(combinedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _stateMachine.TransitionToFailed();
            throw;
        }
        try
        {
            ObjectDisposedException.ThrowIf(_disposed == 1, this);

            if (_context.IsRollbackOnly)
            {
                await RollbackInternalAsync(combinedToken).ConfigureAwait(false);
                throw new TransactionCommitException(
                    $"Transaction '{TransactionId}' cannot be committed because it was marked rollback-only by an inner scope.");
            }

            if (_stateMachine.CurrentState != TransactionState.Active)
            {
                throw new TransactionStateException(_stateMachine.CurrentState, "Commit");
            }

            bool commitDispatched = false;

            try
            {
                combinedToken.ThrowIfCancellationRequested();
                await _context.ExecuteBeforeCommitHooksAsync(combinedToken).ConfigureAwait(false);

                commitDispatched = true;
                await _transaction.CommitAsync(combinedToken).ConfigureAwait(false);

                _stateMachine.TransitionToCommitted();

                double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
                TransactionDiagnostics.RecordCommitted(_context.IsolationLevel, elapsedMs);
                _activity?.SetTag(OutcomeTag, "committed");
                _activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (TransactionStateException)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                _stateMachine.TransitionToFailed();
                if (commitDispatched)
                {
                    double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
                    TransactionDiagnostics.RecordFailed(_context.IsolationLevel, elapsedMs, ex.GetType().Name);
                    _activity?.SetTag(OutcomeTag, "failed");
                    _activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    await _context.ExecuteOnExceptionHooksAsync(ex, CancellationToken.None).ConfigureAwait(false);

                    throw new TransactionCommitException(
                        $"Failed to commit transaction '{TransactionId}'. The operation was canceled after the commit was dispatched to the database engine. The final database state is indeterminate.",
                        ex,
                        isAmbiguous: true);
                }
                throw;
            }
            catch (Exception ex)
            {
                _stateMachine.TransitionToFailed();

                double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
                TransactionDiagnostics.RecordFailed(_context.IsolationLevel, elapsedMs, ex.GetType().Name);
                _activity?.SetTag(OutcomeTag, "failed");
                _activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                await _context.ExecuteOnExceptionHooksAsync(ex, CancellationToken.None).ConfigureAwait(false);

                // An exception during Commit is only ambiguous if the commit command was dispatched to the database engine
                throw new TransactionCommitException(
                    $"Failed to commit transaction '{TransactionId}'. The final database state may be indeterminate.",
                    ex,
                    isAmbiguous: commitDispatched);
            }

            try
            {
                await _context.ExecuteAfterCommitHooksAsync(combinedToken).ConfigureAwait(false);
            }
            catch (Exception hookEx)
            {
                _activity?.SetStatus(ActivityStatusCode.Error, hookEx.Message);
                await _context.ExecuteOnExceptionHooksAsync(hookEx, CancellationToken.None).ConfigureAwait(false);

                throw new TransactionPostCommitException(
                    $"The database transaction '{TransactionId}' was committed successfully, but one or more post-commit hooks failed.",
                    hookEx);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        if (_stateMachine.CurrentState is TransactionState.RolledBack or TransactionState.Disposed)
        {
            return;
        }

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_context.CancellationToken, cancellationToken);
        CancellationToken combinedToken = linkedCts.Token;

        await _gate.WaitAsync(combinedToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed == 1, this);

            await RollbackInternalAsync(combinedToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RollbackInternalAsync(CancellationToken combinedToken)
    {
        if (_stateMachine.CurrentState is TransactionState.RolledBack or TransactionState.Disposed)
        {
            return;
        }

        if (_stateMachine.CurrentState != TransactionState.Active &&
            _stateMachine.CurrentState != TransactionState.Failed &&
            _stateMachine.CurrentState != TransactionState.Created)
        {
            throw new TransactionStateException(_stateMachine.CurrentState, "Rollback");
        }

        try
        {
            await _transaction.RollbackAsync(combinedToken).ConfigureAwait(false);
            _stateMachine.TransitionToRolledBack();

            double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
            TransactionDiagnostics.RecordRolledBack(_context.IsolationLevel, elapsedMs);
            _activity?.SetTag(OutcomeTag, "rolled_back");

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
    public async Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_context.CancellationToken, cancellationToken);
        CancellationToken combinedToken = linkedCts.Token;

        combinedToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(combinedToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed == 1, this);

            if (_stateMachine.CurrentState != TransactionState.Active)
            {
                throw new TransactionStateException(_stateMachine.CurrentState, "CreateSavepoint");
            }

            return await _context.CreateSavepointAsync(name, combinedToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        // Wait for any active commit/rollback/savepoint in progress to finish cleanly before tearing down resources
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
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
                catch (Exception ex)
                {
                    _stateMachine.TransitionToFailed();

                    double elapsedMs = Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds;
                    TransactionDiagnostics.RecordFailed(_context.IsolationLevel, elapsedMs, ex.GetType().Name);
                    _activity?.SetTag(OutcomeTag, "failed");
                    _activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    await _context.ExecuteOnExceptionHooksAsync(ex, CancellationToken.None).ConfigureAwait(false);

                    // If we do not own the connection, and rollback failed during disposal,
                    // the connection may remain in an uncommitted/broken transaction state on the database server.
                    // To prevent connection pool contamination, proactively close the connection.
                    if (!_ownsConnection)
                    {
                        try
                        {
                            await _connection.CloseAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // Suppress secondary failure when closing contaminated pooled connection
                        }
                    }
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

            await _context.DisposeAsync().ConfigureAwait(false);
            _stateMachine.TransitionToDisposed();
        }
        finally
        {
            _gate.Release();
        }
    }
}
