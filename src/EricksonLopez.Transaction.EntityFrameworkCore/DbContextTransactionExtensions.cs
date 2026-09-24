// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using Microsoft.EntityFrameworkCore;

namespace EricksonLopez.Transaction.EntityFrameworkCore;

/// <summary>
/// Provides extension methods for seamlessly integrating Entity Framework Core with the EricksonLopez.Transaction ecosystem.
/// </summary>
public static class DbContextTransactionExtensions
{
    /// <summary>
    /// Enlists the specified <see cref="DbContext"/> into the active transaction managed by <paramref name="transactionContext"/>.
    /// </summary>
    /// <param name="dbContext">The Entity Framework Core database context.</param>
    /// <param name="transactionContext">The active transaction context.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> or <paramref name="transactionContext"/> is <see langword="null"/></exception>
    public static Task UseTransactionAsync(
        this DbContext dbContext, 
        ITransactionContext transactionContext, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(transactionContext);

        return dbContext.Database.UseTransactionAsync(transactionContext.Transaction, cancellationToken);
    }
}
