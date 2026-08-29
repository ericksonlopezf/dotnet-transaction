// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.Diagnostics;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Implements <see cref="ISavepoint"/> over an underlying ADO.NET <see cref="DbTransaction"/>.
/// </summary>
internal sealed class Savepoint : ISavepoint
{
    private readonly DbTransaction _transaction;
    private bool _disposed;

    public Savepoint(DbTransaction transaction, string name)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Savepoint name must not be empty.", nameof(name));
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this savepoint has been rolled back.
    /// </summary>
    public bool IsRolledBack { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this savepoint has been released.
    /// </summary>
    public bool IsReleased { get; private set; }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _transaction.RollbackAsync(Name, cancellationToken).ConfigureAwait(false);
            IsRolledBack = true;
            TransactionDiagnostics.RecordSavepointRolledBack();
        }
        catch (NotSupportedException)
        {
            // Fallback for providers that don't override DbTransaction.RollbackAsync(savepointName)
            if (_transaction.Connection is not null)
            {
                await using DbCommand cmd = _transaction.Connection.CreateCommand();
                cmd.Transaction = _transaction;
                cmd.CommandText = $"ROLLBACK TO SAVEPOINT {Name};";
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                IsRolledBack = true;
                TransactionDiagnostics.RecordSavepointRolledBack();
            }
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _transaction.ReleaseAsync(Name, cancellationToken).ConfigureAwait(false);
            IsReleased = true;
            TransactionDiagnostics.RecordSavepointReleased();
        }
        catch (NotSupportedException)
        {
            // Fallback for providers that support RELEASE SAVEPOINT via SQL (e.g. PostgreSQL, SQLite)
            if (_transaction.Connection is not null)
            {
                try
                {
                    await using DbCommand cmd = _transaction.Connection.CreateCommand();
                    cmd.Transaction = _transaction;
                    cmd.CommandText = $"RELEASE SAVEPOINT {Name};";
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    IsReleased = true;
                    TransactionDiagnostics.RecordSavepointReleased();
                }
                catch
                {
                    // If release savepoint is not supported by database engine (e.g. SQL Server), ignore silently
                }
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
