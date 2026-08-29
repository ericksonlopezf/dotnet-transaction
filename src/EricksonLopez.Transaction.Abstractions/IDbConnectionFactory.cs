// Copyright © Erickson Lopez. MIT License.
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction;

/// <summary>
/// Defines a factory responsible for creating and opening database connections.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Creates and asynchronously opens a new database connection.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains an open <see cref="DbConnection"/>.</returns>
    ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new unopened database connection instance.
    /// </summary>
    /// <returns>A new <see cref="DbConnection"/> instance.</returns>
    DbConnection CreateConnection();
}
