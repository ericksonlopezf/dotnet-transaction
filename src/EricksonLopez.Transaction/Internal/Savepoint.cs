// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.Diagnostics;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Represents an implementation of <see cref="ISavepoint"/> over an underlying ADO.NET <see cref="DbTransaction"/>.
/// </summary>
internal sealed class Savepoint : ISavepoint
{
    private readonly DbTransaction _transaction;
    private readonly IDatabaseDialect _dialect;
    private int _disposed;

    public Savepoint(DbTransaction transaction, string name, IDatabaseDialect dialect)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        Name = ValidateName(name);
    }

    internal static string ValidateName(string name)
    {
        ValidateName(name.AsSpan());
        return name;
    }

    internal static void ValidateName(ReadOnlySpan<char> name)
    {
        if (name.IsWhiteSpace())
        {
            throw new ArgumentException("Savepoint name must not be empty.", nameof(name));
        }

        if (name.Length > 128)
        {
            throw new ArgumentException("Savepoint name must not exceed 128 characters.", nameof(name));
        }

        foreach (char c in name)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_'))
            {
                throw new ArgumentException($"Savepoint name '{name.ToString()}' contains invalid characters. Only alphanumeric ASCII characters and underscores are allowed.", nameof(name));
            }
        }
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
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

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
                string rollbackSql = _dialect.GetSavepointRollbackSql(Name);

                await using DbCommand cmd = _transaction.Connection.CreateCommand();
                cmd.Transaction = _transaction;
                cmd.CommandText = rollbackSql;
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
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        try
        {
            await _transaction.ReleaseAsync(Name, cancellationToken).ConfigureAwait(false);
            IsReleased = true;
            TransactionDiagnostics.RecordSavepointReleased();
        }
        catch (NotSupportedException)
        {
            // Fallback for providers that support RELEASE SAVEPOINT via SQL
            if (_transaction.Connection is not null)
            {
                string? releaseSql = _dialect.GetSavepointReleaseSql(Name);
                if (releaseSql != null)
                {
                    try
                    {
                        await using DbCommand cmd = _transaction.Connection.CreateCommand();
                        cmd.Transaction = _transaction;
                        cmd.CommandText = releaseSql;
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        IsReleased = true;
                        TransactionDiagnostics.RecordSavepointReleased();
                    }
                    catch
                    {
                        // If release savepoint fails on fallback, ignore silently
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }
        return ValueTask.CompletedTask;
    }
}
