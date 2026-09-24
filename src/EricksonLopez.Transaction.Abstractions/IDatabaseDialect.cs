// Copyright © Erickson Lopez. MIT License.
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction;

/// <summary>
/// Provides an abstraction over database-specific syntax and behaviors.
/// </summary>
public interface IDatabaseDialect
{
    /// <summary>
    /// Determines whether this dialect can handle the specified connection.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <returns><see langword="true"/> if the dialect can handle the connection; otherwise, <see langword="false"/>.</returns>
    bool CanHandle(DbConnection connection);

    /// <summary>
    /// Applies a read-only transaction mode to the underlying database transaction, if supported.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="transaction">The database transaction.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplyReadOnlyModeAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the SQL command text to create a savepoint.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    /// <returns>The SQL command text used to create the savepoint.</returns>
    string GetSavepointCreationSql(string savepointName);

    /// <summary>
    /// Gets the SQL command text to rollback to a savepoint.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    /// <returns>The SQL command text used to roll back to the specified savepoint.</returns>
    string GetSavepointRollbackSql(string savepointName);

    /// <summary>
    /// Gets the SQL command text to release a savepoint.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    /// <returns>The SQL command text used to release the savepoint, or <see langword="null"/> if the operation is not supported by the database engine.</returns>
    string? GetSavepointReleaseSql(string savepointName);
}
