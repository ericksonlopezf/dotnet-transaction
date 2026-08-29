// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
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
        Name = ValidateName(name);
    }

    internal static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Savepoint name must not be empty.", nameof(name));
        }

        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                throw new ArgumentException($"Savepoint name '{name}' contains invalid characters. Only alphanumeric characters and underscores are allowed.", nameof(name));
            }
        }

        return name;
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
    [SuppressMessage("csharpsquid", "S2077:Use a parameterized query instead of string formatting", Justification = "Savepoint identifiers cannot be parameterized in SQL syntax and the identifier is validated to contain only alphanumeric characters and underscores.")]
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Savepoint name is validated as a strict alphanumeric identifier.")]
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
    [SuppressMessage("csharpsquid", "S2077:Use a parameterized query instead of string formatting", Justification = "Savepoint identifiers cannot be parameterized in SQL syntax and the identifier is validated to contain only alphanumeric characters and underscores.")]
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Savepoint name is validated as a strict alphanumeric identifier.")]
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
