// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.Diagnostics;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Default implementation of <see cref="ITransactionContext"/>.
/// </summary>
internal sealed class TransactionContext : ITransactionContext
{
    private readonly List<ITransactionEnlistment> _enlistments = new();
    private readonly TransactionStateMachine _stateMachine;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private bool _disposed;

    public TransactionContext(
        Guid transactionId,
        DbConnection connection,
        DbTransaction transaction,
        TransactionIsolationLevel isolationLevel,
        TransactionStateMachine stateMachine,
        CancellationToken cancellationToken)
    {
        TransactionId = transactionId;
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        IsolationLevel = isolationLevel;
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        CancellationToken = cancellationToken;
    }

    /// <inheritdoc/>
    public Guid TransactionId { get; }

    /// <inheritdoc/>
    public DbConnection Connection { get; }

    /// <inheritdoc/>
    public DbTransaction Transaction { get; }

    /// <inheritdoc/>
    public TransactionState State => _stateMachine.CurrentState;

    /// <inheritdoc/>
    public TransactionIsolationLevel IsolationLevel { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ITransactionEnlistment> Enlistments
    {
        get
        {
            lock (_lock)
            {
                return _enlistments.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("csharpsquid", "S2077:Use a parameterized query instead of string formatting", Justification = "Savepoint identifiers cannot be parameterized in SQL syntax and the identifier is validated to contain only alphanumeric characters and underscores.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Savepoint name is validated as a strict alphanumeric identifier.")]
    public async Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Savepoint.ValidateName(name);

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cancellationToken);
        CancellationToken combinedToken = linkedCts.Token;

        try
        {
            await Transaction.SaveAsync(name, combinedToken).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            // Fallback for providers not implementing DbTransaction.SaveAsync
            await using DbCommand cmd = Connection.CreateCommand();
            cmd.Transaction = Transaction;
            cmd.CommandText = $"SAVEPOINT {name};";
            await cmd.ExecuteNonQueryAsync(combinedToken).ConfigureAwait(false);
        }

        TransactionDiagnostics.RecordSavepointCreated();
        return new Savepoint(Transaction, name);
    }

    /// <inheritdoc/>
    public void Enlist(ITransactionEnlistment enlistment)
    {
        ArgumentNullException.ThrowIfNull(enlistment);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _enlistments.Add(enlistment);
        }
    }

    internal async Task ExecuteBeforeCommitHooksAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ITransactionEnlistment> hooks;
        lock (_lock)
        {
            hooks = _enlistments.ToArray();
        }

        foreach (ITransactionEnlistment hook in hooks)
        {
            await hook.BeforeCommitAsync(this, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task ExecuteAfterCommitHooksAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ITransactionEnlistment> hooks;
        lock (_lock)
        {
            hooks = _enlistments.ToArray();
        }

        foreach (ITransactionEnlistment hook in hooks)
        {
            await hook.AfterCommitAsync(this, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task ExecuteAfterRollbackHooksAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ITransactionEnlistment> hooks;
        lock (_lock)
        {
            hooks = _enlistments.ToArray();
        }

        foreach (ITransactionEnlistment hook in hooks)
        {
            try
            {
                await hook.AfterRollbackAsync(this, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Suppress secondary failures during rollback hook executions
            }
        }
    }

    internal async Task ExecuteOnExceptionHooksAsync(Exception exception, CancellationToken cancellationToken)
    {
        IReadOnlyList<ITransactionEnlistment> hooks;
        lock (_lock)
        {
            hooks = _enlistments.ToArray();
        }

        foreach (ITransactionEnlistment hook in hooks)
        {
            try
            {
                await hook.OnExceptionAsync(this, exception, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Suppress secondary failures during exception hook executions
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
