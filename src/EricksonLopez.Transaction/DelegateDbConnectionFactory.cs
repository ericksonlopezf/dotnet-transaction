// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction;

/// <summary>
/// Provides a delegate-based implementation of <see cref="IDbConnectionFactory"/>
/// that wraps caller-supplied factory functions.
/// </summary>
/// <remarks>
/// When constructed with a synchronous delegate, the asynchronous path automatically opens
/// the connection if it is not already open. When constructed with an asynchronous delegate,
/// calling <see cref="CreateConnection"/> throws <see cref="System.NotSupportedException"/>.
/// </remarks>
public sealed class DelegateDbConnectionFactory : IDbConnectionFactory
{
    private readonly Func<CancellationToken, ValueTask<DbConnection>> _asyncFactory;
    private readonly Func<DbConnection> _syncFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateDbConnectionFactory"/> class with an asynchronous factory delegate.
    /// </summary>
    /// <param name="asyncFactory">The asynchronous connection factory function.</param>
    /// <exception cref="ArgumentNullException"><paramref name="asyncFactory"/> is <see langword="null"/></exception>
    public DelegateDbConnectionFactory(Func<CancellationToken, ValueTask<DbConnection>> asyncFactory)
    {
        _asyncFactory = asyncFactory ?? throw new ArgumentNullException(nameof(asyncFactory));
        _syncFactory = () => throw new NotSupportedException("Synchronous connection creation is not supported when configured with an asynchronous factory.");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateDbConnectionFactory"/> class with a synchronous factory delegate.
    /// </summary>
    /// <param name="syncFactory">The synchronous connection factory function.</param>
    /// <exception cref="ArgumentNullException"><paramref name="syncFactory"/> is <see langword="null"/></exception>
    public DelegateDbConnectionFactory(Func<DbConnection> syncFactory)
    {
        _syncFactory = syncFactory ?? throw new ArgumentNullException(nameof(syncFactory));
        _asyncFactory = async cancellationToken =>
        {
            DbConnection conn = _syncFactory();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            return conn;
        };
    }

    /// <inheritdoc/>
    public ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        => _asyncFactory(cancellationToken);

    /// <inheritdoc/>
    public DbConnection CreateConnection()
        => _syncFactory();
}
